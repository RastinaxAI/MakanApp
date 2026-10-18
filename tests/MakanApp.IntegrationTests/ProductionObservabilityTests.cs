using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Storage;
using MakanApp.Api.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ProductionObservabilityTests
{
    [Theory]
    [InlineData("release-ph3.safe_1", true)]
    [InlineData("unsafe/correlation", false)]
    [InlineData("", false)]
    public async Task CorrelationIsConsistentInResponseActivityAndLogScope(string supplied, bool accepted)
    {
        using var activity = new Activity("ph3.correlation-test").Start();
        using var host = CreateHost();
        using var client = Client(host);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ph3-test/observability/ok");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", supplied);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.Equal(accepted, correlation == supplied);
        Assert.InRange(correlation.Length, 1, 64);
        Assert.Equal(correlation, body.GetProperty("correlationId").GetString());
        Assert.Equal(correlation, body.GetProperty("activityCorrelation").GetString());
        var completion = Completion(host);
        Assert.Equal(correlation, completion.Properties["CorrelationId"]);
        Assert.Contains(completion.Scopes, scope => scope is IEnumerable<KeyValuePair<string, object?>> values &&
            values.Any(item => item.Key == "CorrelationId" && Equals(item.Value, correlation)));
        Assert.Equal(body.GetProperty("traceId").GetString(), completion.Properties["TraceId"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedAndMultipleCorrelationHeadersAreReplaced(bool multiple)
    {
        using var host = CreateHost();
        using var client = Client(host);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ph3-test/observability/ok");
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", multiple ? ["first", "second"] : [new string('x', 4096)]);
        using var response = await client.SendAsync(request);
        var correlation = Assert.Single(response.Headers.GetValues("X-Correlation-ID"));
        Assert.InRange(correlation.Length, 1, 64);
        Assert.DoesNotContain(',', correlation);
        Assert.NotEqual("first", correlation);
        Assert.NotEqual("second", correlation);
    }

    [Fact]
    public async Task ExistingKnownErrorKeepsCodeAndStatusWithMatchingProblemCorrelation()
    {
        using var host = CreateHost();
        using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "known-ph3");
        using var response = await client.GetAsync("/ph3-test/observability/known");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(IdentityErrorCodes.AuthRequired, problem.GetProperty("code").GetString());
        Assert.Equal("known-ph3", problem.GetProperty("correlationId").GetString());
        Assert.Equal("known-ph3", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.Equal(401, Completion(host).Properties["StatusCode"]);
    }

    [Theory]
    [InlineData("Production", "known", 401, IdentityErrorCodes.AuthRequired, LogLevel.Warning)]
    [InlineData("Staging", "known", 401, IdentityErrorCodes.AuthRequired, LogLevel.Warning)]
    [InlineData("Production", "otp-invalid", 400, IdentityErrorCodes.OtpInvalid, LogLevel.Warning)]
    [InlineData("Staging", "otp-invalid", 400, IdentityErrorCodes.OtpInvalid, LogLevel.Warning)]
    [InlineData("Production", "phone-invalid", 400, IdentityErrorCodes.PhoneInvalid, LogLevel.Warning)]
    [InlineData("Staging", "phone-invalid", 400, IdentityErrorCodes.PhoneInvalid, LogLevel.Warning)]
    [InlineData("Production", "storage", 503, StorageErrorCodes.FileStorageFailed, LogLevel.Error)]
    [InlineData("Staging", "storage", 503, StorageErrorCodes.FileStorageFailed, LogLevel.Error)]
    [InlineData("Production", "hostile-code", 400, ObservabilityFaultTestController.HostileCode, LogLevel.Warning)]
    [InlineData("Staging", "hostile-code", 400, ObservabilityFaultTestController.HostileCode, LogLevel.Warning)]
    public async Task KnownFailuresPreserveContractsAndLogSafeCodeStatusIdsAndSeverity(
        string environment, string mode, int status, string expectedCode, LogLevel severity)
    {
        using var host = CreateHost(providerOverride: true, environment: environment);
        using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "safe-known-failure");
        using var response = await client.GetAsync("/ph3-test/observability/" + mode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.Equal("safe-known-failure", problem.GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        if (mode != "known")
            Assert.Equal(ObservabilityFaultTestController.KnownPrivateMessage, problem.GetProperty("detail").GetString());

        var logs = host.Services.GetRequiredService<SensitiveLogTestControl>();
        var failure = Assert.Single(logs.Entries(), entry => entry.EventId.Name == "KnownRequestFailure");
        Assert.Equal(severity, failure.Level);
        Assert.Null(failure.ExceptionText);
        Assert.Equal(mode == "hostile-code" ? "UNRECOGNIZED_ERROR" : expectedCode, failure.Properties["ErrorCode"]);
        Assert.Equal(status, failure.Properties["StatusCode"]);
        Assert.Equal("safe-known-failure", failure.Properties["CorrelationId"]);
        Assert.Equal(Completion(host).Properties["TraceId"], failure.Properties["TraceId"]);
        var captured = string.Join('\n', logs.Snapshot()) + JsonSerializer.Serialize(logs.Entries());
        Assert.DoesNotContain(ObservabilityFaultTestController.KnownPrivateMessage, captured, StringComparison.Ordinal);
        Assert.DoesNotContain(ObservabilityFaultTestController.KnownInnerPrivateMarker, captured, StringComparison.Ordinal);
        Assert.DoesNotContain(ObservabilityFaultTestController.HostileCode, captured, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ok", "2xx", 0)]
    [InlineData("known", "4xx", 0)]
    [InlineData("unexpected", "5xx", 1)]
    [InlineData("storage", "5xx", 1)]
    public async Task MetricsUseFinalMappedStatusAndOriginalRouteTemplate(string mode, string statusClass, int errors)
    {
        var measurements = new List<(string Name, long Value, Dictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == MakanApiMetrics.MeterName) observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            lock (measurements) measurements.Add((instrument.Name, value, tags.ToArray().ToDictionary(item => item.Key, item => item.Value)));
        });
        listener.Start();
        using var host = CreateHost();
        using var client = Client(host);
        using var response = await client.GetAsync("/ph3-test/observability/" + mode);
        _ = await response.Content.ReadAsStringAsync();
        var request = Assert.Single(measurements, item => item.Name == "makan.http.server.requests");
        Assert.Equal(1, request.Value);
        Assert.Equal(statusClass, request.Tags["http.response.status_class"]);
        Assert.Equal("ph3-test/observability/{mode}", request.Tags["http.route"]);
        Assert.Equal("GET", request.Tags["http.request.method"]);
        Assert.Equal(errors, measurements.Where(item => item.Name == "makan.http.server.errors").Sum(item => item.Value));
        Assert.Equal(["http.request.method", "http.response.status_class", "http.route"], request.Tags.Keys.Order().ToArray());
    }

    [Fact]
    public async Task CompletionLogUsesOnlyTemplateAndNormalizedMethodNotBodyOrQuery()
    {
        using var host = CreateHost();
        using var client = Client(host);
        using var request = new HttpRequestMessage(new HttpMethod("PH3-ARBITRARY-METHOD"),
            "/ph3-test/observability/method?private=synthetic-query-marker")
        {
            Content = new StringContent("synthetic-body-marker")
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completion = Completion(host);
        Assert.Equal("OTHER", completion.Properties["Method"]);
        Assert.Equal("ph3-test/observability/method", completion.Properties["Route"]);
        Assert.Equal(200, completion.Properties["StatusCode"]);
        Assert.DoesNotContain("synthetic-query-marker", JsonSerializer.Serialize(completion), StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-body-marker", JsonSerializer.Serialize(completion), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    public async Task SensitiveOuterAndInnerExceptionNeverReachFrameworkOrApplicationLogs(string accept)
    {
        using var host = CreateHost(providerOverride: true);
        using var client = Client(host);
        client.DefaultRequestHeaders.Add("Accept", accept);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "safe-error-ph3");
        using var response = await client.GetAsync("/ph3-test/observability/unexpected");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<JsonElement>(payload);
        Assert.Equal("UNEXPECTED_ERROR", problem.GetProperty("code").GetString());
        Assert.Equal("safe-error-ph3", problem.GetProperty("correlationId").GetString());
        Assert.Equal("safe-error-ph3", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        var logs = host.Services.GetRequiredService<SensitiveLogTestControl>();
        var combined = payload + string.Join('\n', logs.Snapshot()) + JsonSerializer.Serialize(logs.Entries());
        Assert.DoesNotContain("synthetic-outer-secret-marker", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-inner-private-file-marker", combined, StringComparison.Ordinal);
        var error = Assert.Single(logs.Entries(), entry => entry.EventId.Name == "UnexpectedError");
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Null(error.ExceptionText);
        Assert.Equal("UNEXPECTED_ERROR", error.Properties["ErrorCode"]);
        Assert.Equal(500, error.Properties["StatusCode"]);
    }

    [Fact]
    public async Task DevelopmentKeepsOriginalDiagnosticsWithoutChangingPublicProblemDetails()
    {
        using var host = CreateHost(providerOverride: true, environment: "Development");
        using var client = Client(host);
        using var response = await client.GetAsync("/ph3-test/observability/unexpected");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("UNEXPECTED_ERROR", problem.GetProperty("code").GetString());
        Assert.DoesNotContain("synthetic-outer-secret-marker", body, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-inner-private-file-marker", body, StringComparison.Ordinal);
        var logs = host.Services.GetRequiredService<SensitiveLogTestControl>();
        Assert.Contains(logs.Entries(), entry =>
            entry.Category.StartsWith("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", StringComparison.Ordinal) &&
            entry.Level == LogLevel.Error &&
            entry.ExceptionText is { } diagnostic && diagnostic.Contains("synthetic-outer-secret-marker", StringComparison.Ordinal) &&
            diagnostic.Contains("synthetic-inner-private-file-marker", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task SensitiveRequestInputsAreAbsentFromFailureLogsAndPublicUnexpectedProblem(string environment)
    {
        string[] markers =
        [
            "synthetic-authorization-token-marker", "synthetic-otp-marker", "synthetic-password-marker",
            "synthetic-service-secret-marker", "synthetic-messenger-content-marker", "synthetic-private-user-marker",
            "synthetic-connection-string-marker", "synthetic-attachment-payload-marker", "synthetic-refresh-cookie-marker"
        ];
        using var host = CreateHost(providerOverride: true, environment: environment);
        using var client = Client(host);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "/ph3-test/observability/private-failure?private=" + markers[6]);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", markers[0]);
        request.Headers.Add("Cookie", "refreshToken=" + markers[8]);
        request.Headers.Add("X-Correlation-ID", "safe-private-request-failure");
        request.Content = JsonContent.Create(new
        {
            otp = markers[1], password = markers[2], secret = markers[3], message = markers[4],
            user = markers[5], connectionString = markers[6], attachment = markers[7]
        });

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.Equal("UNEXPECTED_ERROR", problem.GetProperty("code").GetString());
        Assert.Equal("safe-private-request-failure", problem.GetProperty("correlationId").GetString());
        var logs = host.Services.GetRequiredService<SensitiveLogTestControl>();
        var evidence = body + string.Join('\n', logs.Snapshot()) + JsonSerializer.Serialize(logs.Entries());
        foreach (var marker in markers) Assert.DoesNotContain(marker, evidence, StringComparison.Ordinal);
        var failure = Assert.Single(logs.Entries(), entry => entry.EventId.Name == "UnexpectedError");
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Null(failure.ExceptionText);
        Assert.Equal(500, failure.Properties["StatusCode"]);
        Assert.Equal("safe-private-request-failure", failure.Properties["CorrelationId"]);
        Assert.False(string.IsNullOrWhiteSpace(failure.Properties["TraceId"] as string));
    }

    [Fact]
    public async Task ProductionSqlReadinessFailureLogsDoNotExposeConnectionDetails()
    {
        const string database = "MakanApp_PH3_Synthetic_Private_Database";
        using var host = CreateHost(connectionString:
            "Server=tcp:127.0.0.1,1;Database=" + database + ";Integrated Security=True;Connect Timeout=1;Encrypt=True;TrustServerCertificate=False");
        using var client = Client(host);
        using var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
        var logs = string.Join('\n', host.Services.GetRequiredService<SensitiveLogTestControl>().Snapshot());
        Assert.DoesNotContain(database, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Server=tcp:", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(503, Completion(host).Properties["StatusCode"]);
    }

    [Fact]
    public async Task StartedResponseStillFailsButServerOnlyReceivesSanitizedFailure()
    {
        using var host = CreateHost(providerOverride: true);
        using var client = Client(host);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "safe-started-failure");
        var exception = await Record.ExceptionAsync(async () =>
        {
            using var response = await client.GetAsync("/ph3-test/observability/started");
            _ = await response.Content.ReadAsStringAsync();
        });
        Assert.NotNull(exception);
        var logs = host.Services.GetRequiredService<SensitiveLogTestControl>();
        var combined = exception.ToString() + string.Join('\n', logs.Snapshot());
        Assert.DoesNotContain("synthetic-started-private-marker", combined, StringComparison.Ordinal);
        var failure = Assert.Single(logs.Entries(), entry => entry.EventId.Name == "RequestPipelineFailure");
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Null(failure.ExceptionText);
        Assert.Equal("HTTP_PIPELINE_FAILED", failure.Properties["ErrorCode"]);
        Assert.Equal(200, failure.Properties["StatusCode"]);
        Assert.Equal("safe-started-failure", failure.Properties["CorrelationId"]);
        var completion = Completion(host);
        Assert.Equal(completion.Properties["TraceId"], failure.Properties["TraceId"]);
        Assert.Equal(200, completion.Properties["StatusCode"]);
        Assert.Equal("aborted", completion.Properties["Outcome"]);
        Assert.All(logs.Entries(), entry =>
        {
            if (entry.ExceptionText is { } text)
            {
                Assert.DoesNotContain(" at ", text, StringComparison.Ordinal);
                Assert.DoesNotContain(".cs:line ", text, StringComparison.Ordinal);
            }
        });
    }

    [Fact]
    public async Task DirectBoundaryPropagatesSafeFailureWithoutOriginalInnerAndMeasuresFailure()
    {
        using var services = new ServiceCollection().AddMetrics().AddLogging().BuildServiceProvider();
        var telemetry = new MakanApiMetrics(services.GetRequiredService<IMeterFactory>());
        var failures = new List<string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, observer) =>
        {
            if (instrument.Meter.Name == MakanApiMetrics.MeterName) observer.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name == "makan.http.server.failures")
                failures.Add(tags.ToArray().Single(tag => tag.Key == "outcome").Value?.ToString());
        });
        listener.Start();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        var middleware = new HttpRequestMetricsMiddleware(_ => throw new IOException("synthetic-boundary-private-marker"),
            telemetry, services.GetRequiredService<ILogger<HttpRequestMetricsMiddleware>>());
        var exception = await Assert.ThrowsAsync<SanitizedRequestFailureException>(() => middleware.InvokeAsync(context));
        Assert.Null(exception.InnerException);
        Assert.Null(exception.StackTrace);
        Assert.Equal("HTTP_PIPELINE_FAILED", exception.Message);
        Assert.Equal("HTTP_PIPELINE_FAILED", exception.ToString());
        Assert.DoesNotContain("synthetic-boundary-private-marker", exception.ToString(), StringComparison.Ordinal);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("aborted", Assert.Single(failures));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectBoundaryPreservesRequestedCancellationWithoutOriginalMessage(bool nonCancellationException)
    {
        using var services = new ServiceCollection().AddMetrics().AddLogging().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var middleware = new HttpRequestMetricsMiddleware(_ => Task.FromException(nonCancellationException
                ? new IOException("synthetic-cancel-marker") : new OperationCanceledException("synthetic-cancel-marker")),
            new MakanApiMetrics(services.GetRequiredService<IMeterFactory>()), services.GetRequiredService<ILogger<HttpRequestMetricsMiddleware>>());
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("synthetic-cancel-marker", exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production", false, false)]
    [InlineData("Production", false, true)]
    [InlineData("Staging", false, false)]
    [InlineData("Staging", false, true)]
    [InlineData("Development", true, false)]
    [InlineData("Development", true, true)]
    public void NarrowPolicyCannotBeReenabledByProviderOverrideAndDoesNotHideOtherOperationalLogs(
        string environment, bool rawEnabled, bool childCategory)
    {
        const string framework = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";
        var category = childCategory ? framework + ".SyntheticChild" : framework;
        var logs = new SensitiveLogTestControl();
        var registrations = new ServiceCollection();
        registrations.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new RecordingTestLoggerProvider(logs))
            .AddFilter<RecordingTestLoggerProvider>(category, LogLevel.Trace));
        ProductionLoggingSafety.Configure(registrations, new TestEnvironment { EnvironmentName = environment });
        using var services = registrations.BuildServiceProvider();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        loggerFactory.CreateLogger(category).LogError(new IOException("synthetic-filter-private-marker"), "synthetic raw framework event");
        loggerFactory.CreateLogger("MakanApp.Api.SafeOperations").LogError("synthetic-visible-safe-failure");
        loggerFactory.CreateLogger("MakanApp.Api.SafeOperations").LogCritical("synthetic-visible-safe-critical-failure");
        loggerFactory.CreateLogger("Microsoft.EntityFrameworkCore.Migrations").LogWarning("synthetic-visible-sql-status");
        var captured = string.Join('\n', logs.Snapshot());
        Assert.Equal(rawEnabled, captured.Contains("synthetic-filter-private-marker", StringComparison.Ordinal));
        Assert.Contains("synthetic-visible-safe-failure", captured, StringComparison.Ordinal);
        Assert.Contains(logs.Entries(), entry => entry.Level == LogLevel.Critical &&
            entry.Properties.Values.Any(value => Equals(value, "synthetic-visible-safe-critical-failure")));
        Assert.Contains("synthetic-visible-sql-status", captured, StringComparison.Ordinal);
    }

    private static CapturedLogEntry Completion(WebApplicationFactory<Program> host) =>
        Assert.Single(host.Services.GetRequiredService<SensitiveLogTestControl>().Entries(),
            item => item.EventId.Name == "RequestCompleted");

    private static HttpClient Client(WebApplicationFactory<Program> host) => host.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    private static WebApplicationFactory<Program> CreateHost(
        bool providerOverride = false, string? connectionString = null, string environment = "Production")
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "MakanApp-PH3-" + Guid.NewGuid().ToString("N"));
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:MakanDatabase"] = connectionString ?? "Server=(localdb)\\MSSQLLocalDB;Database=MakanApp_PH3_OnlyTestHost;Integrated Security=True;Encrypt=True;TrustServerCertificate=False",
                ["Identity:SecurityKey"] = new string('P', 32),
                ["Storage:RootPath"] = Path.Combine(testRoot, "storage"),
                ["Security:DataProtection:KeyRingPath"] = Path.Combine(testRoot, "keys"),
                ["Sms:Provider"] = "Unavailable",
                ["Sms:MeliPayamakOtp:ServiceToken"] = string.Empty,
                ["AllowedHosts"] = "localhost",
                ["Cors:AllowedOrigins:0"] = "https://app.example.test",
                ["Hosting:RequireHttps"] = "true",
                ["ReverseProxy:Enabled"] = "true",
                ["ReverseProxy:KnownProxies:0"] = "127.0.0.1",
                ["Swagger:Enabled"] = "false"
            };
            foreach (var (key, value) in values) builder.UseSetting(key, value);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(values));
            if (providerOverride) builder.ConfigureLogging(logging =>
                logging.AddFilter<RecordingTestLoggerProvider>("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.Trace));
            builder.ConfigureTestServices(services =>
            {
                // هیچ worker یا دیتابیس عادی در این تست HTTP لازم نیست.
                var workers = services.Where(item => item.ServiceType == typeof(IHostedService) &&
                    item.ImplementationType?.Namespace?.StartsWith("MakanApp.Infrastructure", StringComparison.Ordinal) == true).ToArray();
                foreach (var worker in workers) services.Remove(worker);
                services.AddSingleton<SensitiveLogTestControl>();
                services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider, RecordingTestLoggerProvider>();
                services.AddControllers().AddApplicationPart(typeof(ObservabilityFaultTestController).Assembly);
            });
        });
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "MakanApp.PH3.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

// این controller فقط با ApplicationPart تست ثبت می‌شود و route محصول نیست.
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("ph3-test/observability")]
public sealed class ObservabilityFaultTestController : ControllerBase
{
    public const string HostileCode = "NOT_A_REAL_ERROR_PRIVATE_CODE_MARKER";
    public const string KnownPrivateMessage = "synthetic-known-private-message";
    public const string KnownInnerPrivateMarker = "synthetic-known-inner-private-marker";

    [HttpPost("private-failure")]
    public IActionResult PrivateFailure() => throw new IOException("synthetic-private-handler-path-marker");

    [HttpGet("started")]
    public async Task Started()
    {
        await Response.StartAsync(HttpContext.RequestAborted);
        await Response.WriteAsync("synthetic-public-started-response");
        await Response.Body.FlushAsync(HttpContext.RequestAborted);
        throw new IOException("synthetic-started-private-marker");
    }

    [HttpGet("{mode}")]
    public IActionResult Get(string mode) => mode switch
    {
        "known" => throw new IdentityException(IdentityErrorCodes.AuthRequired, "ورود لازم است."),
        "otp-invalid" => throw new IdentityException(IdentityErrorCodes.OtpInvalid, KnownPrivateMessage),
        "phone-invalid" => throw new IdentityException(IdentityErrorCodes.PhoneInvalid, KnownPrivateMessage),
        "storage" => throw new StorageException(StorageErrorCodes.FileStorageFailed, KnownPrivateMessage,
            new IOException(KnownInnerPrivateMarker)),
        "hostile-code" => throw new IdentityException(HostileCode, KnownPrivateMessage),
        "unexpected" => throw new InvalidOperationException("synthetic-outer-secret-marker",
            new IOException("synthetic-inner-private-file-marker")),
        _ => Ok(new
        {
            correlationId = HttpContext.TraceIdentifier,
            activityCorrelation = Activity.Current?.GetTagItem("makan.correlation_id"),
            traceId = Activity.Current?.TraceId.ToString()
        })
    };

    [Route("method")]
    public IActionResult AnyMethod() => Ok();
}
