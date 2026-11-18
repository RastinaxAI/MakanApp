using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Ai;
using MakanApp.Application.Identity;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Storage;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Commerce;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Knowledge;
using MakanApp.Infrastructure.Persistence;
using MakanApp.IntegrationTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.KnowledgeIntegrationProbe;

// این پذیرش فقط کانفیگ Test را می‌خواند؛ SQL/Storage به منابع تازهٔ ایزوله هدایت می‌شوند.
internal static class TestRuntimeAcceptance
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } };

    public static async Task<int> RunAsync()
    {
        var evidence = Console.Out;
        var errorOutput = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        var stage = "configuration";
        var passed = true;
        void Write(object value) => evidence.WriteLine(JsonSerializer.Serialize(value));
        try
        {
            var root = Directory.GetCurrentDirectory();
            Require(File.Exists(Path.Combine(root, "MakanApp.sln")), "REPOSITORY_ROOT_REQUIRED");
            var api = Path.Combine(root, "MakanApp.Api");
            var baseFile = Path.Combine(api, "appsettings.json");
            var testFile = Path.Combine(api, "appsettings.Test.json");
            var configuration = new ConfigurationBuilder().AddJsonFile(baseFile).AddJsonFile(testFile).Build();
            var ai = configuration.GetSection(AiServiceOptions.SectionName).Get<AiServiceOptions>()!;
            var knowledge = configuration.GetSection(AiKnowledgeOptions.SectionName).Get<AiKnowledgeOptions>()!;
            var usage = configuration.GetSection(AiUsageOptions.SectionName).Get<AiUsageOptions>() ?? new();
            Require(Enum.IsDefined(usage.Mode), "TEST_AI_USAGE_MODE_INVALID");
            var otp = OtpAuthenticationOptions.Load(configuration, "Test");
            Require(!otp.Enabled && otp.AllowMockAuthentication, "AUTHORIZED_TEST_MOCK_REQUIRED");
            Require(ai.Enabled && knowledge.Enabled && !ai.UseServiceAuthentication && !knowledge.UseServiceAuthentication &&
                ai.ContractVersion == "v1" && knowledge.ContractVersion == "knowledge-v1" && AiKnowledgeOptions.IsValid(knowledge),
                "TEST_RUNTIME_CONFIGURATION_INVALID");
            var approved = Environment.GetEnvironmentVariable("AiLiveProbe__ApprovedOrigin");
            _ = MakanApp.AiIntegrationProbe.LiveAiTargetPolicy.Validate(ai.BaseUrl, true, approved);
            _ = MakanApp.AiIntegrationProbe.LiveAiTargetPolicy.Validate(knowledge.BaseUrl, true, approved);
            var zibal = configuration.GetSection(ZibalOptions.SectionName).Get<ZibalOptions>() ?? new();
            var zibalSelected = configuration["Payments:Provider"] == ZibalPaymentGateway.Name;
            zibal.Validate("Test", zibalSelected);
            Write(new { stage, passed = true, aiServiceAuth = false, knowledgeServiceAuth = false,
                aiTimeoutSeconds = ai.TimeoutSeconds, knowledge.StatusPollAttempts, knowledge.StatusPollIntervalMilliseconds,
                knowledge.StatusPollTimeoutSeconds, zibalConfigured = zibalSelected && zibal.IsComplete, usageMode = usage.Mode.ToString() });

            using var factory = MakanAppWebApplicationFactory.CreateKnowledgeProbe();
            string isolatedConnection;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                Require(db.Database.GetDbConnection().Database.StartsWith(MakanAppWebApplicationFactory.KnowledgeProbeDatabasePrefix,
                    StringComparison.Ordinal), "ISOLATED_DATABASE_REQUIRED");
                isolatedConnection = db.Database.GetConnectionString()!;
            }
            await ((IAsyncLifetime)factory).InitializeAsync();
            var cleanupIdentities = new ConcurrentDictionary<(Guid, int), byte>();
            var ragCalls = 0;
            var transportCalls = 0;
            try
            {
                // سازندهٔ fixture رفتار Real OTP تست‌های قدیمی را انتخاب می‌کند؛ این میزبان مشخصاً Test/Mock است.
                Environment.SetEnvironmentVariable("Otp__Enabled", "false");
                Environment.SetEnvironmentVariable("Otp__AllowMockAuthentication", "true");
                using var application = factory.WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Test");
                    builder.ConfigureAppConfiguration((_, source) => source.AddJsonFile(baseFile).AddJsonFile(testFile)
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:MakanDatabase"] = isolatedConnection,
                            ["Storage:RootPath"] = factory.StorageRootPath
                        }));
                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<AiServiceOptions>(); services.AddSingleton(ai);
                        services.RemoveAll<IAiRuntimeAvailability>(); services.AddSingleton<IAiRuntimeAvailability, AiRuntimeAvailability>();
                        services.PostConfigure<AiKnowledgeOptions>(options => configuration.GetSection(AiKnowledgeOptions.SectionName).Bind(options));
                        services.RemoveAll<StorageOptions>(); services.AddSingleton(new StorageOptions { RootPath = factory.StorageRootPath });
                        services.RemoveAll<IAiKnowledgeClient>();
                        services.RemoveAll<ILoggerProvider>();
                        services.AddSingleton<ILogger<AiKnowledgeClient>>(new SafeKnowledgeLogger(Write));
                        services.AddHttpClient<IAiKnowledgeClient, AiKnowledgeClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
                            .ConfigurePrimaryHttpMessageHandler(() => new RuntimeObservationHandler(
                                new KnowledgeAcceptanceHandler(new HttpClientHandler { AllowAutoRedirect = false },
                                    (id, version) => cleanupIdentities.ContainsKey((id, version))),
                                path => { Interlocked.Increment(ref transportCalls); if (path.EndsWith("/rag/answer", StringComparison.Ordinal)) Interlocked.Increment(ref ragCalls); }));
                    });
                });
                await using (var scope = application.Services.CreateAsyncScope())
                {
                    Require(scope.ServiceProvider.GetRequiredService<IHostEnvironment>().IsEnvironment("Test"), "TEST_ENVIRONMENT_REQUIRED");
                    var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                    Require(db.Database.GetConnectionString() == isolatedConnection, "ISOLATED_DATABASE_REQUIRED");
                    var actual = scope.ServiceProvider.GetRequiredService<IOptions<AiKnowledgeOptions>>().Value;
                    Require(actual.StatusPollAttempts == knowledge.StatusPollAttempts && actual.MaximumContentBytes == knowledge.MaximumContentBytes &&
                        actual.MaximumResponseBytes == knowledge.MaximumResponseBytes && actual.StatusPollIntervalMilliseconds == knowledge.StatusPollIntervalMilliseconds &&
                        actual.StatusPollTimeoutSeconds == knowledge.StatusPollTimeoutSeconds && actual.TimeoutSeconds == knowledge.TimeoutSeconds,
                        "KNOWLEDGE_LIMITS_MUST_BE_PRESERVED");
                    Require(!scope.ServiceProvider.GetRequiredService<OtpOptions>().Enabled, "MOCK_TEST_HOST_REQUIRED");
                }
                stage = "test_startup_health";
                using (var health = application.CreateClient())
                    foreach (var path in new[] { "/health/live", "/health/ready" })
                    {
                        using var response = await health.GetAsync(path);
                        Write(new { stage, path, status = (int)response.StatusCode });
                        response.EnsureSuccessStatusCode();
                    }

                var actor = await KnowledgeProbeProgram.CreateActor(application, 1, mockOtp: true);
                using var client = actor.Client;
                await KnowledgeProbeProgram.Contract(factory, SubscriptionOwner.Individual(actor.Id));
                var source = await KnowledgeProbeProgram.CreateSource(client,
                    Encoding.UTF8.GetBytes("Synthetic QA only: planet Aster has three moons, named Luma, Neri and Osa."), "text/plain");
                cleanupIdentities.TryAdd((source.SourceId, source.Revision), 0);
                if (usage.IsUnmetered)
                {
                    // قرارداد فقط برای ایجاد فایل QA بود؛ AI با قرارداد لغوشده و بدون Entitlement فعال آزموده می‌شود.
                    await using var scope = application.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                    (await db.Subscriptions.SingleAsync(s => s.OwnerId == actor.Id)).Cancel(factory.Clock.GetUtcNow().UtcDateTime);
                    await db.SaveChangesAsync();
                }
                var ingestAccepted = false;
                try
                {
                    stage = "test_knowledge_ingest";
                    var mutation = new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "پذیرش منبع شخصی QA همین اجرا");
                    using (var response = await client.PostAsJsonAsync(SourcePath(source, "ingest"), mutation, Json))
                    {
                        await Observe(response, stage, HttpStatusCode.Accepted, Write);
                        ingestAccepted = true;
                    }
                    stage = "test_knowledge_status";
                    using (var response = await client.GetAsync(SourcePath(source, "status") + "?wait=true"))
                    {
                        await Observe(response, stage, HttpStatusCode.OK, Write);
                        var state = (await response.Content.ReadFromJsonAsync<KnowledgeProcessingStatus>(Json))!;
                        Require(state.State == "Ready", "KNOWLEDGE_READY_REQUIRED");
                        Write(new { stage = "test_knowledge_ready", passed = true });
                    }
                    var query = new KnowledgeQueryCommand(Guid.NewGuid(), "How many moons does the synthetic planet Aster have?",
                        [new(source.SourceId, source.Revision)], Language: "en");
                    stage = "test_knowledge_retrieve";
                    using (var response = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, Json))
                    {
                        await Observe(response, stage, HttpStatusCode.OK, Write);
                        var result = (await response.Content.ReadFromJsonAsync<KnowledgeRetrieveResponse>(Json))!;
                        Require(result.Results.Count > 0 && result.Results.All(x => x.SourceId == source.SourceId && x.Version == source.Revision), "RETRIEVAL_ALLOWLIST_INVALID");
                    }
                    query = query with { ClientOperationId = Guid.NewGuid() };
                    stage = "test_knowledge_rag";
                    using (var response = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, Json))
                    {
                        await Observe(response, stage, HttpStatusCode.OK, Write);
                        var result = (await response.Content.ReadFromJsonAsync<KnowledgeRagResponse>(Json))!;
                        Require(result.Status == "Ok" && result.Claims.Count > 0 && result.Citations.Count > 0 &&
                            result.Citations.All(x => x.SourceId == source.SourceId && x.Version == source.Revision), "RAG_GROUNDING_INVALID");
                    }
                    using (var replay = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, Json))
                        await Observe(replay, "test_rag_completed_replay", HttpStatusCode.Conflict, Write);
                    await using (var scope = application.Services.CreateAsyncScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                        var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == query.ClientOperationId);
                        var consumed = await db.UsageLedgerEntries.CountAsync(l => l.ReservationId == reservation.Id && l.Status == UsageReservationStatus.Consumed);
                        Require(reservation.OwnerType == SubscriptionOwnerType.Individual && reservation.OwnerId == actor.Id &&
                            reservation.Status == UsageReservationStatus.Consumed && consumed == 1 && ragCalls == 1 && !await db.Memberships.AnyAsync(), "PERSONAL_USAGE_NOT_SAFE");
                        Require(reservation.IsUnmetered == usage.IsUnmetered && (!usage.IsUnmetered || reservation.SubscriptionId is null &&
                            !await db.Subscriptions.AnyAsync(s => s.OwnerId == actor.Id && s.Status == SubscriptionStatus.Active)), "KNOWLEDGE_ACCOUNTING_MODE_NOT_SAFE");
                        Write(new { stage = "test_personal_usage", passed = true, consumed, ragCalls, syntheticOrganizationRoles = 0,
                            accountingMode = reservation.AccountingMode.ToString(), noActiveSubscription = usage.IsUnmetered });
                    }
                    var other = await KnowledgeProbeProgram.CreateActor(application, 2, mockOtp: true);
                    using var otherClient = other.Client;
                    var before = transportCalls;
                    using (var denied = await otherClient.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query with { ClientOperationId = Guid.NewGuid() }, Json))
                        await Observe(denied, "test_foreign_source_denied", HttpStatusCode.NotFound, Write);
                    Require(transportCalls == before, "FOREIGN_SOURCE_REACHED_UPSTREAM");
                }
                finally
                {
                    if (ingestAccepted)
                    {
                        try
                        {
                            using var latest = await client.GetAsync($"/api/v1/sources/{source.SourceId}");
                            latest.EnsureSuccessStatusCode();
                            var current = (await latest.Content.ReadFromJsonAsync<EducationalSourceResult>(Json))!;
                            using var deleted = await client.PostAsJsonAsync(SourcePath(source, "delete"),
                                new KnowledgeMutationCommand(Guid.NewGuid(), current.Version, "پاک‌سازی فقط منبع QA ساخته‌شده در همین اجرا"), Json);
                            await Observe(deleted, "test_own_qa_cleanup", HttpStatusCode.OK, Write);
                            Require((await deleted.Content.ReadFromJsonAsync<KnowledgeProcessingStatus>(Json))!.State == "Deleted", "QA_CLEANUP_NOT_CONFIRMED");
                            Write(new { stage = "test_qa_cleanup_confirmed", source.SourceId, source.Revision, oldResourcesTouched = false });
                        }
                        catch (Exception exception) { passed = false; Write(new { stage = "test_qa_cleanup_failed", errorType = exception.GetType().Name, source.SourceId, source.Revision }); }
                    }
                }
            }
            finally
            {
                await ((IAsyncLifetime)factory).DisposeAsync();
                Write(new { stage = "test_isolation_cleanup", normalDatabaseChanged = false, productionConfigurationChanged = false });
            }
        }
        catch (Exception exception) { passed = false; Write(new { stage, passed = false, errorType = exception.GetType().Name }); }
        finally { Console.SetOut(evidence); Console.SetError(errorOutput); }
        Write(new { stage = "test_runtime_acceptance_completed", passed });
        return passed ? 0 : 1;
    }

    private static string SourcePath(EducationalSourceResult source, string action) =>
        $"/api/v1/sources/{source.SourceId}/versions/{source.Revision}/knowledge/{action}";

    private static async Task Observe(HttpResponseMessage response, string stage, HttpStatusCode expected, Action<object> write)
    {
        write(new { stage, status = (int)response.StatusCode, passed = response.StatusCode == expected });
        Require(response.StatusCode == expected, "TEST_RUNTIME_HTTP_FAILURE");
        await response.Content.LoadIntoBufferAsync();
    }

    private static void Require(bool condition, string code)
    { if (!condition) throw new InvalidOperationException(code); }

    private sealed class RuntimeObservationHandler(HttpMessageHandler inner, Action<string> observed) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { observed(request.RequestUri!.AbsolutePath); return base.SendAsync(request, ct); }
    }

    private sealed class SafeKnowledgeLogger(Action<object> write) : ILogger<AiKnowledgeClient>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is not IEnumerable<KeyValuePair<string, object?>> entries) return;
            var fields = entries.ToDictionary(pair => pair.Key, pair => pair.Value);
            write(new { stage = "test_knowledge_transport", status = fields.GetValueOrDefault("StatusCode"),
                requestId = fields.GetValueOrDefault("RequestId"), correlationId = fields.GetValueOrDefault("CorrelationId"),
                elapsedMs = fields.GetValueOrDefault("DurationMs") });
        }
    }
}
