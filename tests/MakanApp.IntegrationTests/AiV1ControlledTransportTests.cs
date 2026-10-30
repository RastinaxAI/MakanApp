using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MakanApp.Application.Ai;
using MakanApp.Infrastructure.Ai;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class AiV1ControlledTransportTests
{
    [Fact]
    public async Task ValidExistingServiceCredentialReachesBodyValidation()
    {
        await using var server = await ControlledAiV1Server.StartAsync();
        using var http = new HttpClient(); var credential = await Credentials(server).GetCredentialAsync(default);
        using var request = new HttpRequestMessage(HttpMethod.Post, server.BaseUrl + "/api/v1/ai/chat") { Content = JsonContent.Create(new { }) };
        request.Headers.Authorization = new("Bearer", credential.Token); request.Headers.Add("X-AI-Contract-Version", "v1");
        using var response = await http.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("AI_INVALID_REQUEST", Assert.Single(server.Events).ErrorCode); Assert.Equal(0, server.BusinessCalls);
    }

    [Theory]
    [InlineData("missing", 401)] [InlineData("malformed", 401)] [InlineData("signature", 401)] [InlineData("expired", 401)]
    [InlineData("scope", 403)] [InlineData("version", 422)] [InlineData("correlation", 422)]
    [InlineData("issuer", 401)] [InlineData("audience", 401)] [InlineData("subject", 401)] [InlineData("kid", 401)]
    public async Task AuthAndHeaderNegativesNeverReachBusiness(string variant, int expected)
    {
        await using var server = await ControlledAiV1Server.StartAsync(); using var http = new HttpClient();
        var credential = await Credentials(server).GetCredentialAsync(default); var token = credential.Token;
        if (variant is "expired" or "scope" or "issuer" or "audience" or "subject" or "kid") token = Modify(token, server.Key, variant);
        if (variant == "signature") token = token[..(token.LastIndexOf('.') + 1)] + Encode(RandomNumberGenerator.GetBytes(32));
        if (variant == "malformed") token = "invalid-token";
        using var request = new HttpRequestMessage(HttpMethod.Post, server.BaseUrl + "/api/v1/ai/chat")
        { Content = JsonContent.Create(new { requestId = "controlled-request", correlationId = "controlled-correlation" }) };
        if (variant != "missing") request.Headers.Authorization = new("Bearer", token);
        request.Headers.Add("X-AI-Contract-Version", variant == "version" ? "v2" : "v1");
        if (variant == "correlation") request.Headers.Add("X-Correlation-ID", "different-correlation");
        using var response = await http.SendAsync(request); Assert.Equal(expected, (int)response.StatusCode); Assert.Equal(0, server.BusinessCalls);
    }

    [Fact]
    public async Task AllTwelveOperationsExecuteRealClientAndServiceJwtAgainstLoopbackContract()
    {
        await using var server = await ControlledAiV1Server.StartAsync(); using var http = new HttpClient();
        var client = Client(server, http);
        foreach (var request in Requests())
        {
            var response = await Invoke(client, request);
            Assert.Equal(request.RequestId, response.RequestId); Assert.Equal(request.CorrelationId, response.CorrelationId);
            Assert.Equal("education-fast", response.Metadata.ModelAlias);
        }
        Assert.Equal(12, server.BusinessCalls); Assert.Equal(12, server.Events.Count); Assert.All(server.Events, e => Assert.Equal(200, e.Status));
        foreach (var captured in server.Requests)
        {
            foreach (var key in new[] { "selectedSourceIds", "sourceManifest", "chunks", "documents", "payer", "subscriptionId" }) Assert.False(captured.ContainsKey(key));
            var json = captured.ToJsonString(); Assert.False(json.Contains(server.Key, StringComparison.Ordinal)); Assert.False(json.Contains("Bearer ", StringComparison.Ordinal));
        }
    }

    [Theory] [InlineData("alias")] [InlineData("correlation")] [InlineData("grounding")] [InlineData("schema")]
    public async Task InvalidControlledResponsesAreRejectedWithoutPartialResult(string problem)
    {
        await using var server = await ControlledAiV1Server.StartAsync(); server.InvalidResponse = problem;
        using var http = new HttpClient(); var client = Client(server, http);
        var error = await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync((AiChatRequest)Requests()[0], default));
        Assert.Equal(AiServiceErrorCodes.ResponseInvalid, error.Code); Assert.Null(error.InnerException); Assert.Equal(1, server.BusinessCalls);
    }

    [Theory]
    [InlineData(429, AiServiceErrorCodes.RateLimited)] [InlineData(503, AiServiceErrorCodes.ServiceUnavailable)]
    [InlineData(504, AiServiceErrorCodes.ServiceTimeout)] [InlineData(502, AiServiceErrorCodes.ResponseInvalid)]
    public async Task ControlledFailuresMapSafelyWithoutRetry(int status, string code)
    {
        await using var server = await ControlledAiV1Server.StartAsync(); server.NextStatus = status;
        using var http = new HttpClient(); var client = Client(server, http);
        var error = await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync((AiChatRequest)Requests()[0], default));
        Assert.Equal(code, error.Code); Assert.Equal(status, error.UpstreamStatusCode); Assert.Equal(1, server.BusinessCalls);
        Assert.False(error.ToString().Contains(server.Key, StringComparison.Ordinal)); Assert.DoesNotContain("Controlled failure", error.Message);
    }
    [Fact]
    public async Task ControlledTimeoutUsesApprovedMappingAndNoRetry()
    {
        await using var server = await ControlledAiV1Server.StartAsync(); server.ResponseDelay = TimeSpan.FromSeconds(2);
        using var http = new HttpClient(); var client = Client(server, http, 1);
        var error = await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync((AiChatRequest)Requests()[0], default));
        Assert.Equal(AiServiceErrorCodes.ServiceTimeout, error.Code); Assert.Equal(1, server.BusinessCalls); Assert.Null(error.InnerException);
    }

    public static HmacAiServiceCredentialProvider Credentials(ControlledAiV1Server server, TimeProvider? clock = null) =>
        new(new AiServiceAuthenticationOptions { CurrentKey = server.Key, CurrentKeyId = ControlledAiV1Server.KeyId }, clock ?? TimeProvider.System);
    public static AiServiceClient Client(ControlledAiV1Server server, HttpClient http, int timeout = 35)
    {
        var options = new AiServiceOptions { Enabled = true, BaseUrl = server.BaseUrl, TimeoutSeconds = timeout };
        return new(http, Credentials(server), options, new AiServiceCircuitBreaker(options, TimeProvider.System));
    }
    public static IAiServiceRequest[] Requests()
    {
        var user = Guid.NewGuid().ToString("D"); var org = Guid.NewGuid().ToString("D"); var cls = Guid.NewGuid().ToString("D"); var student = Guid.NewGuid().ToString("D");
        var id = Guid.NewGuid().ToString("N"); var corr = "controlled-" + id; var scope = new AiScope(org, org, cls, student);
        var teacherClass = new AiTeacherClassScope(org, org, cls); var teacherStudent = new AiTeacherStudentScope(org, org, cls, student);
        var classData = new AiTeacherClassData(cls, [], [], [], [], [], [], null); var studentData = new AiTeacherStudentData(student, [], [], [], [], []);
        return [
            new AiChatRequest(id, corr, null, new(user,"Student"), scope, new("پرسش ساختگی"), AiAuthorizedContext.Empty),
            new AiStudentAnalysisRequest(id, corr, new(user,"Student"), scope, AiAuthorizedContext.Empty),
            new AiStudyPlanRequest(id, corr, new(user,"Student"), scope, AiAuthorizedContext.Empty, [], new(null,null)),
            new AiHomeworkExplanationRequest(id, corr, new(user,"Student"), scope, new Dictionary<string,JsonElement>(), "پرسش", AiAuthorizedContext.Empty),
            new AiExamPreparationRequest(id, corr, new(user,"Student"), scope, new Dictionary<string,JsonElement>(), AiAuthorizedContext.Empty, AiAuthorizedContext.Empty),
            new AiParentChildSummaryRequest(id, corr, new(user,"Parent"), scope, AiAuthorizedContext.Empty),
            new AiTeacherClassSummaryRequest(id, corr, new(user,"Teacher"), teacherClass, classData),
            new AiTeacherStudentInsightRequest(id, corr, new(user,"Teacher"), teacherStudent, studentData),
            new AiTeacherActivitySuggestionsRequest(id, corr, new(user,"Teacher"), teacherClass, classData, "هدف", new(null,[],null)),
            new AiManagerSchoolSummaryRequest(id, corr, new(user,"Manager"), new(org,org), new(org,[],[],[],[],[],[],null)),
            new AiManagerClassInsightRequest(id, corr, new(user,"Manager"), new(org,org,cls), new(cls,[],[],[],[],[],[],null)),
            new AiManagerStudentInsightRequest(id, corr, new(user,"Manager"), new(org,org,cls,student), new(student,[],[],[],[],[])) ];
    }
    public static async Task<IAiServiceResponse> Invoke(IAiServiceClient client, IAiServiceRequest r) => r switch
    {
        AiChatRequest x => await client.ChatAsync(x, default), AiStudentAnalysisRequest x => await client.AnalyzeStudentAsync(x, default),
        AiStudyPlanRequest x => await client.CreateStudyPlanAsync(x, default), AiHomeworkExplanationRequest x => await client.ExplainHomeworkAsync(x, default),
        AiExamPreparationRequest x => await client.PrepareExamAsync(x, default), AiParentChildSummaryRequest x => await client.SummarizeChildAsync(x, default),
        AiTeacherClassSummaryRequest x => await client.SummarizeTeacherClassAsync(x, default), AiTeacherStudentInsightRequest x => await client.GetTeacherStudentInsightAsync(x, default),
        AiTeacherActivitySuggestionsRequest x => await client.SuggestTeacherActivitiesAsync(x, default), AiManagerSchoolSummaryRequest x => await client.SummarizeManagerSchoolAsync(x, default),
        AiManagerClassInsightRequest x => await client.GetManagerClassInsightAsync(x, default), AiManagerStudentInsightRequest x => await client.GetManagerStudentInsightAsync(x, default),
        _ => throw new InvalidOperationException()
    };
    private static string Modify(string token, string key, string variant)
    {
        var parts = token.Split('.'); var header = JsonNode.Parse(Decode(parts[0]))!.AsObject(); var payload = JsonNode.Parse(Decode(parts[1]))!.AsObject();
        switch (variant)
        {
            case "expired": payload["exp"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(); break;
            case "scope": payload.Remove("scope"); break;
            case "issuer": payload["iss"] = "invalid-issuer"; break;
            case "audience": payload["aud"] = "invalid-audience"; break;
            case "subject": payload["sub"] = "invalid-subject"; break;
            case "kid": header["kid"] = "unknown-key-id"; break;
        }
        var unsigned = Encode(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." + Encode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        return unsigned + "." + Encode(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.ASCII.GetBytes(unsigned)));
    }
    private static string Encode(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string Decode(string s) => Encoding.UTF8.GetString(Convert.FromBase64String(s.Replace('-','+').Replace('_','/') + new string('=', (4-s.Length%4)%4)));
}
