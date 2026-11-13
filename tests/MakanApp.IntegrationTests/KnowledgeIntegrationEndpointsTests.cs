using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Infrastructure.Knowledge;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task KnowledgeOpenApiDescribesExactVersionsStatesAndBodyIntentWithoutUnsupportedBusinessHeaders()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Swagger:Enabled"] = "true" })));
        using var client = host.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json"); response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement; var paths = root.GetProperty("paths");
        Assert.Contains(AiServiceErrorCodes.PersonalContextContractRequired,
            paths.GetProperty("/api/v1/ai/chat").GetProperty("post").GetProperty("description").GetString());
        var retrieve = paths.GetProperty("/api/v1/ai/knowledge/retrieve").GetProperty("post");
        if (retrieve.TryGetProperty("parameters", out var parameters))
            Assert.DoesNotContain(parameters.EnumerateArray(), value => value.GetProperty("name").GetString() == "X-AI-Operation-Id");
        Assert.True(retrieve.GetProperty("responses").TryGetProperty("422", out _));
        var schemas = root.GetProperty("components").GetProperty("schemas");
        Assert.Contains(schemas.GetProperty("KnowledgeProcessingStatus").GetProperty("properties").GetProperty("state").GetProperty("enum").EnumerateArray(),
            value => value.GetString() == "ReindexRequired");
        Assert.Equal(1, schemas.GetProperty("KnowledgeSourceVersion").GetProperty("properties").GetProperty("version").GetProperty("minimum").GetInt32());
        Assert.Equal(32, schemas.GetProperty("KnowledgeQueryCommand").GetProperty("properties").GetProperty("sources").GetProperty("maxItems").GetInt32());
    }

    [Fact]
    public async Task KnowledgePublishedPersonalSourceIngestsReplaysRetrievesAndRagConsumesOnlyOnce()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId), 2);
        var source = await SourcePublish(owner, await SourceCreate(owner, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(owner, true)).Id)));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        var mutation = new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "آزمون ایزوله");
        using (var first = await client.PostAsJsonAsync(KnowledgePath(source, "ingest"), mutation, JsonOptions)) Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        using (var replay = await client.PostAsJsonAsync(KnowledgePath(source, "ingest"), mutation, JsonOptions)) Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Single(control.Staged);
        using (var status = await client.GetAsync(KnowledgePath(source, "status")))
        { status.EnsureSuccessStatusCode(); Assert.Equal("Ready", (await status.Content.ReadFromJsonAsync<KnowledgeProcessingStatus>(JsonOptions))!.State); }
        using (var projection = await client.GetAsync($"/api/v1/sources/{source.SourceId}/processing"))
        {
            projection.EnsureSuccessStatusCode();
            var ready = (await projection.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!;
            Assert.True(ready.AiReady); Assert.True(ready.Capabilities.CanUseForAi); Assert.Equal("Ready", ready.AiReadiness);
            Assert.True(projection.Headers.CacheControl!.NoStore);
        }
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش ساختگی", [new(source.SourceId, 1)], IncludeQuotePreview: true);
        using (var retrieval = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, JsonOptions))
        {
            retrieval.EnsureSuccessStatusCode(); var result = (await retrieval.Content.ReadFromJsonAsync<KnowledgeRetrieveResponse>(JsonOptions))!;
            Assert.Equal(source.SourceId, Assert.Single(result.Results).SourceId);
        }
        query = query with { ClientOperationId = Guid.NewGuid() };
        using (var rag = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions))
        { rag.EnsureSuccessStatusCode(); Assert.Equal("Ok", (await rag.Content.ReadFromJsonAsync<KnowledgeRagResponse>(JsonOptions))!.Status); }
        using (var replay = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions))
        { Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Contains(AiInvocationErrorCodes.Completed, await replay.Content.ReadAsStringAsync()); }
        Assert.Equal(1, control.RagCalls);
        Assert.All(control.Allowlists, ids => Assert.Equal(new[] { new KnowledgeSourceVersion(source.SourceId, 1) }, ids));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.UsageReservations.CountAsync(x => x.ConsumerUserId == user.UserId && x.Status == UsageReservationStatus.Consumed));
        Assert.True(await db.AdminAuditLogs.AnyAsync(x => x.ActorUserId == user.UserId && x.Action == "knowledge.ai.ingest"));
    }

    [Fact]
    public async Task KnowledgeForeignSourceIsConcealedBeforeHttpPinAndQuota()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        using var stranger = factory.CreateClient(); var outsider = await CreateMessagingUserAsync(stranger);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId));
        var source = await SourcePublish(owner, await SourceCreate(owner, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(owner, true)).Id)));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, stranger);
        using var denied = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve",
            new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش", [new(source.SourceId, 1)]), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); Assert.Equal(0, control.Calls);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.Set<AiSourceSelectionOperation>().AnyAsync(x => x.UserId == outsider.UserId));
        Assert.False(await db.UsageReservations.AnyAsync(x => x.ConsumerUserId == outsider.UserId));
    }

    [Fact]
    public async Task KnowledgeDraftAndCommerciallyUnauthorizedSourceNeverReachAi()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId), grantAi: false);
        var source = await SourceCreate(owner, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(owner, true)).Id));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        var command = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش", [new(source.SourceId, 1)]);
        using (var draft = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", command, JsonOptions)) Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        source = await SourcePublish(owner, source);
        using (var denied = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", command, JsonOptions))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, control.Calls);
    }

    [Fact]
    public async Task KnowledgeEnrollmentRevocationBlocksSamePinnedVersionWithoutDeletingRemoteBytes()
    {
        using var s = await CreateOfficialScenarioAsync(false); await KnowledgeContract(SubscriptionOwner.Organization(s.OrganizationId));
        var source = await SourcePublish(s.Teacher, await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId)));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control);
        using var teacher = KnowledgeClient(host, s.Teacher); using var student = KnowledgeClient(host, s.Student);
        using (var ingest = await teacher.PostAsJsonAsync(KnowledgePath(source, "ingest"), new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "آزمون"), JsonOptions)) ingest.EnsureSuccessStatusCode();
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش", [new(source.SourceId, 1)]);
        using (var allowed = await student.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, JsonOptions)) allowed.EnsureSuccessStatusCode();
        var calls = control.Calls;
        await factory.EndEnrollmentAsync(s.EnrollmentId);
        using var denied = await student.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); Assert.Equal(calls, control.Calls); Assert.Single(control.Staged);
    }

    [Fact]
    public async Task KnowledgeRevocationDuringRetrievalConcealsReturnedPrivateEvidence()
    {
        using var s = await CreateOfficialScenarioAsync(false); await KnowledgeContract(SubscriptionOwner.Organization(s.OrganizationId));
        var source = await SourcePublish(s.Teacher, await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId)));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control);
        using var teacher = KnowledgeClient(host, s.Teacher); using var student = KnowledgeClient(host, s.Student);
        using (var ingest = await teacher.PostAsJsonAsync(KnowledgePath(source, "ingest"), new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "آزمون"), JsonOptions)) ingest.EnsureSuccessStatusCode();
        control.BeforeRetrievalResponse = () => factory.EndEnrollmentAsync(s.EnrollmentId);
        using var denied = await student.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve",
            new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش", [new(source.SourceId, 1)]), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); Assert.DoesNotContain("Synthetic knowledge evidence", await denied.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task KnowledgeCrossOrganizationAndEndedTeacherAssignmentNeverExpandScope()
    {
        using var a = await CreateOfficialScenarioAsync(false); using var b = await CreateOfficialScenarioAsync(false);
        await KnowledgeContract(SubscriptionOwner.Organization(a.OrganizationId));
        var source = await SourcePublish(a.Teacher, await SourceCreate(a.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(a.Teacher, false)).Id, a.ClassId)));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control);
        using var foreign = KnowledgeClient(host, b.Teacher); using var teacher = KnowledgeClient(host, a.Teacher);
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش", [new(source.SourceId, 1)]);
        using (var denied = await foreign.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, JsonOptions)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await factory.EndTeacherAssignmentAsync(a.TeacherAssignmentId);
        using (var denied = await teacher.GetAsync(KnowledgePath(source, "status"))) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(0, control.Calls);
    }

    [Theory]
    [InlineData("delete", "Deleted", 200)]
    [InlineData("supersede", "Superseded", 200)]
    [InlineData("reindex", "Queued", 202)]
    public async Task KnowledgeLifecycleIsExplicitAndRepeatedIntentUsesSameSourceVersion(string action, string state, int status)
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId));
        var source = await SourcePublish(owner, await SourceCreate(owner, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(owner, true)).Id)));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        using (var ingest = await client.PostAsJsonAsync(KnowledgePath(source, "ingest"), new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "آزمون"), JsonOptions)) ingest.EnsureSuccessStatusCode();
        var command = new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "آزمون lifecycle");
        for (var i = 0; i < 2; i++)
        {
            using var response = await client.PostAsJsonAsync(KnowledgePath(source, action), command, JsonOptions);
            Assert.Equal((HttpStatusCode)status, response.StatusCode);
            Assert.Equal(state, (await response.Content.ReadFromJsonAsync<KnowledgeProcessingStatus>(JsonOptions))!.State);
        }
        Assert.Single(control.Staged);
    }

    [Fact]
    public async Task KnowledgeRagLostAckKeepsUnknownAndRetryNeverInvokesAgain()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId));
        var source = await SourcePublish(owner, await SourceCreate(owner, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(owner, true)).Id)));
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        using (var ingest = await client.PostAsJsonAsync(KnowledgePath(source, "ingest"), new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "آزمون"), JsonOptions)) ingest.EnsureSuccessStatusCode();
        control.LoseRagResponse = true;
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش", [new(source.SourceId, 1)], IncludeQuotePreview: true);
        using (var failed = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions)) Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        var calls = control.Calls;
        using (var replay = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions))
        { Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Contains(AiInvocationErrorCodes.Unknown, await replay.Content.ReadAsStringAsync()); }
        Assert.Equal(calls, control.Calls); Assert.Equal(1, control.RagCalls);
    }

    private WebApplicationFactory<Program> KnowledgeHost(KnowledgeTransport control) => factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
    {
        services.RemoveAll<IKnowledgeRuntimeAvailability>(); services.AddSingleton<IKnowledgeRuntimeAvailability, KnowledgeTestRuntime>();
        services.RemoveAll<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
        services.AddSingleton(factory.Services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        services.RemoveAll<MakanApp.Application.Identity.IIdentitySecurity>();
        services.AddSingleton(factory.Services.GetRequiredService<MakanApp.Application.Identity.IIdentitySecurity>());
        services.RemoveAll<IAiKnowledgeClient>();
        services.AddScoped<IAiKnowledgeClient>(provider => new AiKnowledgeClient(new HttpClient(control, disposeHandler: false),
            Options.Create(new AiKnowledgeOptions { Enabled = true, BaseUrl = "https://knowledge.integration.test" }),
            provider.GetRequiredService<IKnowledgeRuntimeAvailability>(), provider.GetRequiredService<IAiServiceCredentialProvider>(),
            factory.Clock, NullLogger<AiKnowledgeClient>.Instance));
    }));
    private static HttpClient KnowledgeClient(WebApplicationFactory<Program> host, HttpClient authorized)
    { var client = host.CreateClient(); client.DefaultRequestHeaders.Authorization = authorized.DefaultRequestHeaders.Authorization; return client; }
    private static string KnowledgePath(EducationalSourceResult source, string action) => $"/api/v1/sources/{source.SourceId}/versions/{source.Revision}/knowledge/{action}";
    private async Task KnowledgeContract(SubscriptionOwner owner, long requests = 1000, bool grantAi = true)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = factory.Clock.GetUtcNow().UtcDateTime;
        var plan = Plan.Create("knowledge-qa." + Guid.NewGuid().ToString("N"), "قرارداد ایزوله دانش", now.AddDays(-2), owner.Type);
        var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-2)); version.SetMonthlyTerms(BillingCalendar.Gregorian, "isolated-qa-not-for-sale", 80, 95);
        version.SetPrice(1000, CurrencyCode.Irr);
        foreach (var entitlement in new[] { EntitlementKey.Academic, EntitlementKey.Assignments, EntitlementKey.Exams }) version.SetEntitlement(entitlement, true);
        if (owner.Type == SubscriptionOwnerType.Individual) version.SetEntitlement(EntitlementKey.PersonalSources, true);
        version.SetEntitlement(EntitlementKey.Ai, grantAi);
        foreach (var quota in new[] { QuotaKey.SourceCount, QuotaKey.ActiveClasses, QuotaKey.ActiveLearners }) version.SetQuota(quota, 100);
        version.SetQuota(QuotaKey.StorageBytes, 1000000); version.SetQuota(QuotaKey.AiTextRequests, requests);
        version.Publish(now.AddDays(-1)); db.Plans.Add(plan); db.PlanVersions.Add(version);
        db.Subscriptions.Add(Subscription.CreateMonthly(owner, version, now.AddHours(-1), 0, now, null, true)); await db.SaveChangesAsync();
    }
    private sealed class KnowledgeTestRuntime : IKnowledgeRuntimeAvailability { public void EnsureEnabled() { } }
    private sealed class KnowledgeTransport : HttpMessageHandler
    {
        public ConcurrentDictionary<(Guid, int), (string Hash, string Title)> Staged { get; } = new();
        public ConcurrentDictionary<(Guid, int), string> States { get; } = new();
        public List<KnowledgeSourceVersion[]> Allowlists { get; } = [];
        public List<KnowledgeScope> IngestScopes { get; } = [];
        public int Calls { get; private set; }
        public int RagCalls { get; private set; }
        public bool LoseRagResponse { get; set; }
        public Func<Task>? BeforeRetrievalResponse { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Null(request.Headers.Authorization);
            Calls++;
            var path = request.RequestUri!.AbsolutePath; var parts = path.Split('/');
            if (path.Contains("/sources/", StringComparison.Ordinal))
            {
                var key = (Guid.Parse(parts[5]), int.Parse(parts[7])); var action = parts[8];
                if (action == "ingest")
                {
                    var content = Assert.IsType<MultipartFormDataContent>(request.Content).ToArray();
                    var metadata = (await content[0].ReadFromJsonAsync<KnowledgeIngestMetadata>(cancellationToken: ct))!;
                    IngestScopes.Add(metadata.Scope);
                    Assert.Equal(metadata.ContentHash, Convert.ToHexStringLower(SHA256.HashData(await content[1].ReadAsByteArrayAsync(ct))));
                    var created = Staged.TryAdd(key, (metadata.ContentHash, metadata.Title));
                    if (Staged[key].Hash != metadata.ContentHash) return KnowledgeResponse(409, new { error = new { code = KnowledgeErrorCodes.VersionConflict } });
                    if (created) States[key] = "Ready";
                    return KnowledgeResponse(created ? 202 : 200, new KnowledgeProcessingStatus(key.Item1, key.Item2, created ? "Queued" : States[key], DateTimeOffset.UtcNow));
                }
                if (action == "delete") States[key] = "Deleted";
                if (action == "supersede") States[key] = "Superseded";
                if (action == "reindex") return KnowledgeResponse(202, new KnowledgeProcessingStatus(key.Item1, key.Item2, "Queued", DateTimeOffset.UtcNow));
                return KnowledgeResponse(200, new KnowledgeProcessingStatus(key.Item1, key.Item2, States.GetValueOrDefault(key, "NotIngested"), DateTimeOffset.UtcNow));
            }
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var root = body.RootElement;
            var allowed = root.GetProperty("allowedSourceVersions").Deserialize<KnowledgeSourceVersion[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Allowlists.Add(allowed);
            var source = allowed[0]; var citation = new KnowledgeCitation(source.SourceId, source.Version, "chunk-1", Staged[(source.SourceId, source.Version)].Title, null, null, null,
                root.GetProperty("includeQuotePreview").GetBoolean() ? "Synthetic" : null);
            var requestId = root.GetProperty("requestId").GetString()!; var correlation = root.GetProperty("correlationId").GetString()!;
            if (path.EndsWith("/retrieve", StringComparison.Ordinal))
            {
                if (BeforeRetrievalResponse is not null) await BeforeRetrievalResponse();
                return KnowledgeResponse(200, new KnowledgeRetrieveResponse(requestId, correlation, "Ok", [new(source.SourceId, source.Version, 1, "chunk-1", "Synthetic knowledge evidence", citation)]));
            }
            RagCalls++;
            Assert.Empty(root.GetProperty("businessContext").EnumerateArray());
            if (LoseRagResponse) throw new HttpRequestException("خطای ساختگی انتقال؛ بدنهٔ خصوصی ثبت نشود.");
            return KnowledgeResponse(200, new KnowledgeRagResponse(requestId, correlation, "Ok", "Synthetic claim.", [new("Synthetic claim.", [citation])], [citation]));
        }
        private static HttpResponseMessage KnowledgeResponse<T>(int status, T value) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json") };
    }
}
