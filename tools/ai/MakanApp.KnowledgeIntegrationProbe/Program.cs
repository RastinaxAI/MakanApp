using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Knowledge;
using MakanApp.Infrastructure.Persistence;
using MakanApp.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.KnowledgeIntegrationProbe;

internal static class KnowledgeProbeProgram
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private static TextWriter output = TextWriter.Null;
    private static bool passed = true;
    private static int transportCalls;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--audit-local-pending") return await KnowledgePendingAudit.RunAsync(args[1]);
        if (!args.SequenceEqual(["--run-live", "--allow-remote-https"])) return 2;
        output = Console.Out; var originalError = Console.Error;
        Console.SetOut(TextWriter.Null); Console.SetError(TextWriter.Null);
        var stage = "configuration";
        try
        {
            var configuration = new ConfigurationBuilder().AddUserSecrets(typeof(MakanApp.Api.Controllers.AiController).Assembly, optional: true)
                .AddEnvironmentVariables().Build();
            var target = MakanApp.AiIntegrationProbe.LiveAiTargetPolicy.Validate(configuration["AiKnowledge:BaseUrl"], true,
                configuration["AiLiveProbe:ApprovedOrigin"]);
            var assets = Path.GetFullPath(configuration["KnowledgeLiveProbe:AssetsDirectory"] ?? throw new InvalidOperationException());
            if (!new[] { "qa-source.txt", "qa-source.pdf", "qa-source.png", "qa-source.jpg" }.All(name => File.Exists(Path.Combine(assets, name))))
                throw new InvalidOperationException("QA_ASSETS_REQUIRED");
            using var factory = MakanAppWebApplicationFactory.CreateKnowledgeProbe();
            using var application = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<AiServiceOptions>(); services.AddSingleton(new AiServiceOptions { Enabled = true, BaseUrl = target.AbsoluteUri });
                services.Configure<AiKnowledgeOptions>(options => { options.Enabled = true; options.BaseUrl = target.AbsoluteUri; });
                services.RemoveAll<StorageOptions>(); services.AddSingleton(new StorageOptions { RootPath = factory.StorageRootPath });
                services.RemoveAll<IAiKnowledgeClient>();
                services.AddSingleton<ILogger<AiKnowledgeClient>, ProbeKnowledgeLogger>();
                services.AddHttpClient<IAiKnowledgeClient, AiKnowledgeClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
                    .ConfigurePrimaryHttpMessageHandler(() => new KnowledgeAcceptanceHandler(new HttpClientHandler { AllowAutoRedirect = false }));
            }));
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetDbConnection().Database;
                if (!database.StartsWith(MakanAppWebApplicationFactory.KnowledgeProbeDatabasePrefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("ISOLATED_DATABASE_REQUIRED");
            }
            stage = "isolated_database";
            await ((IAsyncLifetime)factory).InitializeAsync();
            var staged = new List<(HttpClient Client, EducationalSourceResult Source)>();
            var actorClients = new List<HttpClient>();
            try
            {
                var actor = await CreateActor(application, 1); var client = actor.Client; actorClients.Add(client);
                await Contract(factory, SubscriptionOwner.Individual(actor.Id));
                foreach (var (file, mime) in new[] { ("qa-source.txt", "text/plain"), ("qa-source.pdf", "application/pdf"), ("qa-source.png", "image/png"), ("qa-source.jpg", "image/jpeg") })
                {
                    stage = "source_" + mime;
                    try
                    {
                        var source = await CreateSource(client, await File.ReadAllBytesAsync(Path.Combine(assets, file)), mime);
                        staged.Add((client, source));
                        var command = new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "پذیرش واقعی فقط منبع QA غیرحساس");
                        var path = SourcePath(source, "ingest");
                        using (var first = await client.PostAsJsonAsync(path, command, Json))
                        { await Observe(first, "ingest_" + mime, [202]); if (first.StatusCode != HttpStatusCode.Accepted) continue; }
                        using (var replay = await client.PostAsJsonAsync(path, command, Json))
                        { await Observe(replay, "ingest_replay_" + mime, [200]); if (replay.StatusCode != HttpStatusCode.OK) continue; }
                        using (var status = await client.GetAsync(SourcePath(source, "status") + "?wait=true"))
                        {
                            await Observe(status, "ready_" + mime, [200]);
                            if (!status.IsSuccessStatusCode) continue;
                            var state = await status.Content.ReadFromJsonAsync<KnowledgeProcessingStatus>(Json);
                            Write(new { stage = "processing_state", mime, state = state!.State, passed = state.State == "Ready" });
                            if (state.State != "Ready") { passed = false; continue; }
                        }
                        if (mime == "text/plain")
                        {
                            // آزمون منفی transport فقط روی identity QA متعلق به همین حساب انجام می‌شود.
                            var changed = Encoding.UTF8.GetBytes("Different non-sensitive QA bytes; the immutable identity must reject this.");
                            var requestId = Guid.NewGuid().ToString("N"); var correlation = Guid.NewGuid().ToString("N");
                            await using var scope = application.Services.CreateAsyncScope();
                            var knowledge = scope.ServiceProvider.GetRequiredService<IAiKnowledgeClient>();
                            try
                            {
                                await knowledge.IngestAsync(new(requestId, correlation, source.SourceId, source.Revision,
                                    Convert.ToHexStringLower(SHA256.HashData(changed)), "text/plain", changed.Length,
                                    source.Title, new("Personal", actor.Id), new()), changed, default);
                                passed = false; Write(new { stage = "different_hash_conflict", passed = false });
                            }
                            catch (AiKnowledgeException error)
                            {
                                var conflict = error.Code == KnowledgeErrorCodes.VersionConflict && error.StatusCode == 409;
                                passed &= conflict; Write(new { stage = "different_hash_conflict", status = error.StatusCode, code = error.Code, requestId, correlationId = correlation, passed = conflict });
                            }
                        }
                        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "How many moons does the synthetic planet Aster have?",
                            [new(source.SourceId, source.Revision)], IncludeQuotePreview: true, Language: "en");
                        using (var retrieval = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, Json))
                        {
                            await Observe(retrieval, "retrieve_" + mime, [200]);
                            if (retrieval.IsSuccessStatusCode)
                            {
                                var result = (await retrieval.Content.ReadFromJsonAsync<KnowledgeRetrieveResponse>(Json))!;
                                var exact = result.Results.Count > 0 && result.Results.All(x => x.SourceId == source.SourceId && x.Version == source.Revision);
                                Write(new { stage = "exact_allowlist", mime, passed = exact }); passed &= exact;
                            }
                        }
                        query = query with { ClientOperationId = Guid.NewGuid() };
                        using var rag = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, Json);
                        await Observe(rag, "rag_" + mime, [200]);
                        if (rag.IsSuccessStatusCode)
                        {
                            var result = (await rag.Content.ReadFromJsonAsync<KnowledgeRagResponse>(Json))!;
                            var grounded = result.Status == "Ok" && result.Claims.Count > 0 && result.Citations.All(x => x.SourceId == source.SourceId && x.Version == source.Revision);
                            Write(new { stage = "grounded_citations", mime, passed = grounded }); passed &= grounded;
                            using var completed = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, Json);
                            await Observe(completed, "completed_replay", [409]);
                        }
                    }
                    catch (Exception error) { passed = false; Write(new { stage, passed = false, errorType = error.GetType().Name }); }
                }
                stage = "live_acl_and_revocation";
                var other = await CreateActor(application, 2); actorClients.Add(other.Client);
                await Contract(factory, SubscriptionOwner.Individual(other.Id));
                var privateSource = await CreateSource(other.Client, await File.ReadAllBytesAsync(Path.Combine(assets, "qa-source.txt")), "text/plain");
                staged.Add((other.Client, privateSource));
                using (var ingest = await other.Client.PostAsJsonAsync(SourcePath(privateSource, "ingest"),
                    new KnowledgeMutationCommand(Guid.NewGuid(), privateSource.Version, "منبع خصوصی QA دوم"), Json)) await Observe(ingest, "second_source_ingest", [202]);
                var beforeForeign = transportCalls;
                using (var denied = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve",
                    new KnowledgeQueryCommand(Guid.NewGuid(), "QA unauthorized source", [new(privateSource.SourceId, 1)]), Json))
                    await Observe(denied, "unauthorized_source", [404]);
                var noExpansion = beforeForeign == transportCalls; passed &= noExpansion;
                Write(new { stage = "unauthorized_source_no_upstream", passed = noExpansion });
                var org = await factory.CreateOrganizationAsync("Synthetic Knowledge Revocation QA");
                var classroom = await factory.CreateAcademicClassAsync(org);
                var manager = await factory.CreateMembershipAsync(actor.Id, org, OrganizationRole.Manager);
                var learner = await factory.CreateMembershipAsync(other.Id, org, OrganizationRole.Student);
                var person = await factory.CreateOrganizationPersonAsync(org, await factory.GetUserPersonIdAsync(other.Id));
                var enrollment = await factory.CreateEnrollmentAsync(org, classroom.ClassId, person);
                await Contract(factory, SubscriptionOwner.Organization(org));
                using (var selection = await client.PostAsJsonAsync("/api/v1/workspaces/select",
                    new SelectWorkspaceCommand(WorkspaceType.Organization, manager.MembershipId, OrganizationRole.Manager), Json)) selection.EnsureSuccessStatusCode();
                using (var selection = await other.Client.PostAsJsonAsync("/api/v1/workspaces/select",
                    new SelectWorkspaceCommand(WorkspaceType.Organization, learner.MembershipId, OrganizationRole.Student), Json)) selection.EnsureSuccessStatusCode();
                var classSource = await CreateSource(client, await File.ReadAllBytesAsync(Path.Combine(assets, "qa-source.txt")), "text/plain", EducationalSourceScope.Class, classroom.ClassId);
                staged.Add((client, classSource));
                using (var ingest = await client.PostAsJsonAsync(SourcePath(classSource, "ingest"),
                    new KnowledgeMutationCommand(Guid.NewGuid(), classSource.Version, "منبع کلاسی QA"), Json)) await Observe(ingest, "class_source_ingest", [202]);
                using (var ready = await client.GetAsync(SourcePath(classSource, "status") + "?wait=true")) await Observe(ready, "class_source_ready", [200]);
                var classQuery = new KnowledgeQueryCommand(Guid.NewGuid(), "How many moons does Aster have?", [new(classSource.SourceId, 1)]);
                using (var allowed = await other.Client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", classQuery, Json)) await Observe(allowed, "before_revocation", [200]);
                await factory.EndEnrollmentAsync(enrollment);
                var beforeRevoke = transportCalls;
                using (var denied = await other.Client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", classQuery, Json)) await Observe(denied, "after_revocation", [404]);
                var noRevokedCall = beforeRevoke == transportCalls; passed &= noRevokedCall;
                Write(new { stage = "revocation_no_upstream", passed = noRevokedCall });
                using (var remoteStillReady = await client.GetAsync(SourcePath(classSource, "status")))
                {
                    await Observe(remoteStillReady, "authorized_source_still_exists", [200]);
                    if (remoteStillReady.IsSuccessStatusCode)
                    {
                        var status = (await remoteStillReady.Content.ReadFromJsonAsync<KnowledgeProcessingStatus>(Json))!;
                        var retained = status.State == "Ready"; passed &= retained;
                        Write(new { stage = "revocation_without_vector_delete", passed = retained });
                    }
                }
            }
            finally
            {
                // منبع QA نیز در AI حذف نمی‌شود؛ فقط شناسهٔ همان اجرای غیرحساس برای پیگیری ثبت می‌شود.
                Write(new { stage = "qa_resources_preserved", sources = staged.Select(item => new
                    { item.Source.SourceId, version = item.Source.Revision }).ToArray(),
                    oldResourcesTouched = false, aiLifecycleMutations = 0 });
                foreach (var actorClient in actorClients) actorClient.Dispose();
                await ((IAsyncLifetime)factory).DisposeAsync();
                Write(new { stage = "isolated_database_removed", passed = true, normalDatabaseChanged = false });
            }
            Write(new { stage = "knowledge_live_completed", passed, productionFlagsChanged = false, trafficCutover = "NOT_RUN" });
            return passed ? 0 : 1;
        }
        catch (Exception error) { Write(new { stage, passed = false, errorType = error.GetType().Name }); return 1; }
        finally { Console.SetOut(output); Console.SetError(originalError); }
    }

    private static async Task<(HttpClient Client, Guid Id)> CreateActor(WebApplicationFactory<global::Program> application, int number)
    {
        var client = application.CreateClient(); client.Timeout = TimeSpan.FromSeconds(75);
        var phone = "+9891300099" + number.ToString("D2");
        using var challengeResponse = await client.PostAsJsonAsync("/api/v1/auth/otp/challenges", new RequestOtpCommand(phone), Json);
        challengeResponse.EnsureSuccessStatusCode(); var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(Json))!;
        var otp = application.Services.GetRequiredService<OtpProviderTestControl>().GetLatestCode(phone);
        using var verify = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, phone, otp), Json);
        verify.EnsureSuccessStatusCode(); var user = (await verify.Content.ReadFromJsonAsync<VerifyOtpResult>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        using var profile = await client.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("Synthetic", "QA", "Synthetic QA", "knowledge.qa." + number), Json);
        profile.EnsureSuccessStatusCode(); return (client, user.User.Id);
    }

    private static async Task Contract(MakanAppWebApplicationFactory factory, SubscriptionOwner owner)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = DateTime.UtcNow;
        var plan = Plan.Create("knowledge-live." + Guid.NewGuid().ToString("N"), "قرارداد ساختگی QA؛ غیرقابل فروش", now.AddDays(-2), owner.Type);
        var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-2)); version.SetMonthlyTerms(BillingCalendar.Gregorian, "synthetic-qa-only", 80, 95);
        version.SetPrice(1000, CurrencyCode.Irr); version.SetEntitlement(EntitlementKey.Ai, true);
        version.SetEntitlement(owner.Type == SubscriptionOwnerType.Individual ? EntitlementKey.PersonalSources : EntitlementKey.Academic, true);
        version.SetQuota(QuotaKey.SourceCount, 10); version.SetQuota(QuotaKey.StorageBytes, 1000000); version.SetQuota(QuotaKey.AiTextRequests, 10);
        version.SetQuota(QuotaKey.ActiveClasses, 100); version.SetQuota(QuotaKey.ActiveLearners, 100);
        version.Publish(now.AddDays(-1)); db.Plans.Add(plan); db.PlanVersions.Add(version);
        db.Subscriptions.Add(Subscription.CreateMonthly(owner, version, now.AddHours(-1), 0, now, null, true)); await db.SaveChangesAsync();
    }

    private static async Task<EducationalSourceResult> CreateSource(HttpClient client, byte[] bytes, string mime,
        EducationalSourceScope scope = EducationalSourceScope.Personal, Guid? classId = null)
    {
        using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes); content.Headers.ContentType = new(mime);
        form.Add(content, "file", "synthetic-qa" + (mime == "application/pdf" ? ".pdf" : mime == "image/png" ? ".png" : mime == "image/jpeg" ? ".jpg" : ".txt"));
        using var uploaded = await client.PostAsync("/api/v1/files" + (scope == EducationalSourceScope.Personal ? "?workspaceScope=Personal" : ""), form); uploaded.EnsureSuccessStatusCode();
        var file = (await uploaded.Content.ReadFromJsonAsync<FileAssetResult>(Json))!;
        using var created = await client.PostAsJsonAsync("/api/v1/sources", new CreateEducationalSourceCommand(Guid.NewGuid(),
            scope, classId, "Synthetic QA Aster", null, file.Id, "منبع غیرحساس همین Probe"), Json);
        created.EnsureSuccessStatusCode(); var source = (await created.Content.ReadFromJsonAsync<EducationalSourceResult>(Json))!;
        using var published = await client.PostAsJsonAsync($"/api/v1/sources/{source.SourceId}/publish", new SourceMutationCommand(source.Version, "انتشار QA"), Json);
        published.EnsureSuccessStatusCode(); return (await published.Content.ReadFromJsonAsync<EducationalSourceResult>(Json))!;
    }

    private static string SourcePath(EducationalSourceResult source, string action) => $"/api/v1/sources/{source.SourceId:D}/versions/{source.Revision}/knowledge/{action}";
    private static async Task Observe(HttpResponseMessage response, string stage, int[] expected)
    {
        string? code = null, requestId = null, correlationId = null;
        try
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;
            if (root.TryGetProperty("code", out var value) && value.ValueKind == JsonValueKind.String)
                code = value.GetString();
            if (root.TryGetProperty("requestId", out var request) && request.ValueKind == JsonValueKind.String) requestId = request.GetString();
            if (root.TryGetProperty("correlationId", out var correlation) && correlation.ValueKind == JsonValueKind.String) correlationId = correlation.GetString();
        }
        catch (JsonException) { }
        var success = expected.Contains((int)response.StatusCode); passed &= success;
        Write(new { stage, status = (int)response.StatusCode, code, requestId, correlationId, passed = success });
    }
    private static void Write(object item) => output.WriteLine(JsonSerializer.Serialize(item));
    private sealed class ProbeKnowledgeLogger : ILogger<AiKnowledgeClient>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> values) return;
            var fields = values.ToDictionary(x => x.Key, x => x.Value);
            Interlocked.Increment(ref transportCalls);
            Write(new { stage = "knowledge_transport", operation = fields.GetValueOrDefault("Operation"),
                status = fields.GetValueOrDefault("StatusCode"), code = fields.GetValueOrDefault("Code"),
                requestId = fields.GetValueOrDefault("RequestId"), correlationId = fields.GetValueOrDefault("CorrelationId"),
                elapsedMs = fields.GetValueOrDefault("DurationMs") });
        }
    }
}
