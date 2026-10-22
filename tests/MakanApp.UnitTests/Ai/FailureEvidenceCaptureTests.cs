using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.AiIntegrationProbe;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class FailureEvidenceCaptureTests
{
    private const string PrivateCanary = "unit-private-educational-canary";
    private const string CredentialCanary = "unit-only-credential-canary";
    private const string OriginalIdentifier = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void ExistingSelfCheck_RunsWithoutSecretsConfigurationOrNetwork() =>
        FailureEvidenceCapture.VerifySanitization();

    [Fact]
    public async Task Export_PreservesSafeEvidenceWithoutPrivateTextCredentialsOrOriginalIdentifiers()
    {
        var capture = new FailureEvidenceCapture(IsSafe);
        for (var index = 0; index < 3; index++)
            await CaptureAsync(capture, index);

        var output = capture.Export();

        Assert.DoesNotContain(PrivateCanary, output);
        Assert.DoesNotContain(CredentialCanary, output);
        Assert.DoesNotContain(OriginalIdentifier, output);
        using var document = JsonDocument.Parse(output);
        var cases = document.RootElement.GetProperty("cases");
        Assert.Equal(3, cases.GetArrayLength());
        foreach (var item in cases.EnumerateArray())
        {
            Assert.Equal(504, item.GetProperty("aiStatus").GetInt32());
            Assert.Equal("AI_PROVIDER_TIMEOUT", item.GetProperty("aiErrorCode").GetString());
            Assert.Equal("AI_SERVICE_TIMEOUT", item.GetProperty("backendCode").GetString());
            Assert.StartsWith("trace-request-", item.GetProperty("requestId").GetString());
            Assert.True(item.GetProperty("errorRequestIdMatches").GetBoolean());
            var data = item.GetProperty("requestJson").GetProperty("data");
            Assert.Equal(17.25m, data.GetProperty("score").GetDecimal());
            Assert.Equal(30m, data.GetProperty("maxScore").GetDecimal());
        }
    }

    [Theory]
    [InlineData("requestId")]
    [InlineData("correlationId")]
    public async Task Request_RejectsSensitiveTraceBeforeItCanBecomeEvidence(string field)
    {
        var capture = new FailureEvidenceCapture(IsSafe);
        capture.Begin("parent_child_summary", "/api/v1/ai/parents/child-summary");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://ai.example.test/api/v1/ai/parents/child-summary")
        {
            Content = JsonContent.Create(new Dictionary<string, string>
            {
                ["requestId"] = field == "requestId" ? PrivateCanary : "safe-request",
                ["correlationId"] = field == "correlationId" ? PrivateCanary : "safe-correlation"
            })
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            capture.RequestAsync(request, CancellationToken.None));

        Assert.Equal("UNSAFE_DIAGNOSTIC_TRACE", error.Message);
        Assert.DoesNotContain(PrivateCanary, error.Message);
    }

    [Fact]
    public async Task Export_RejectsUnsafeMetadataInsteadOfReturningSensitiveOutput()
    {
        var capture = new FailureEvidenceCapture(IsSafe);
        for (var index = 0; index < 3; index++)
            await CaptureAsync(capture, index, unsafeMetadata: true);

        var error = Assert.Throws<InvalidOperationException>(() => capture.Export());

        Assert.Equal("DIAGNOSTIC_SANITIZATION_FAILED", error.Message);
        Assert.DoesNotContain(PrivateCanary, error.Message);
    }

    private static bool IsSafe(string value) =>
        !value.Contains(PrivateCanary, StringComparison.Ordinal) &&
        !value.Contains(CredentialCanary, StringComparison.Ordinal);

    private static async Task CaptureAsync(FailureEvidenceCapture capture, int index, bool unsafeMetadata = false)
    {
        // تمام داده‌ها canary ساختگی‌اند؛ handler شبکه‌ای یا تنظیمات محیط ساخته نمی‌شود.
        var requestId = "trace-request-" + index;
        capture.Begin("parent_child_summary", "/api/v1/ai/parents/child-summary");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://ai.example.test/api/v1/ai/parents/child-summary")
        {
            Content = JsonContent.Create(new
            {
                requestId,
                correlationId = "trace-correlation-" + index,
                actor = new { userId = OriginalIdentifier, role = "Parent" },
                data = new { text = PrivateCanary, credential = CredentialCanary, score = 17.25m, maxScore = 30m }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CredentialCanary);
        await capture.RequestAsync(request, CancellationToken.None);
        using var response = new HttpResponseMessage(HttpStatusCode.GatewayTimeout)
        {
            Content = JsonContent.Create(new
            {
                error = new { code = "AI_PROVIDER_TIMEOUT", message = PrivateCanary, requestId }
            })
        };
        if (unsafeMetadata)
            response.Content.Headers.ContentType!.Parameters.Add(new NameValueHeaderValue("diagnostic", PrivateCanary));
        await capture.ResponseAsync(response, CancellationToken.None);
        capture.Backend(504, JsonSerializer.Serialize(new { code = "AI_SERVICE_TIMEOUT", detail = PrivateCanary }),
            Stopwatch.GetTimestamp());
    }
}
