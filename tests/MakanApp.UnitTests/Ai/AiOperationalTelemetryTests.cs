using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using MakanApp.Application.Ai;
using MakanApp.Infrastructure.Ai;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiOperationalTelemetryTests
{
    private const string CredentialSentinel = "synthetic-credential-never-log";
    private const string PromptSentinel = "synthetic-private-prompt-never-log";
    private const string ProviderSentinel = "synthetic-provider-body-never-log";

    [Fact]
    public async Task Success_CreatesChildActivityAndSafeStructuredLog()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = Listen(activities);
        using var parent = new Activity("backend-test").Start();
        var request = Request();
        var body = "{\"requestId\":\"" + request.RequestId + "\",\"correlationId\":\"" + request.CorrelationId +
            "\",\"answer\":{\"text\":\"ok\",\"language\":\"en\"},\"insights\":[],\"risks\":[]," +
            "\"recommendations\":[],\"sources\":[],\"metadata\":{\"modelAlias\":\"synthetic\",\"processingTimeMs\":1}}";
        var logger = new CaptureLogger();
        using var http = new HttpClient(new ResponseHandler(HttpStatusCode.OK, body));
        var options = Options();
        var client = new AiServiceClient(http, new CredentialProvider(), options,
            new AiServiceCircuitBreaker(options, TimeProvider.System), logger);

        await client.ChatAsync(request, CancellationToken.None);

        var activity = Assert.Single(activities, item => Equals(item.GetTagItem("request.id"), request.RequestId));
        Assert.Equal(parent.Id, activity.ParentId);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Equal(request.CorrelationId, activity.GetTagItem("correlation.id"));
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
        Assert.Contains(logger.Entries, item => item.Level == LogLevel.Information);
        Assert.Contains(logger.Scopes, item => item.Contains(request.RequestId, StringComparison.Ordinal));
        AssertSafe(activity, logger);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(422)]
    [InlineData(429)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public async Task Failure_PreservesStatusAndLogsErrorWithoutExceptionOrPayload(int status)
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = Listen(activities);
        var request = Request();
        var logger = new CaptureLogger();
        using var http = new HttpClient(new ResponseHandler((HttpStatusCode)status, ProviderSentinel));
        var options = Options();
        var client = new AiServiceClient(http, new CredentialProvider(), options,
            new AiServiceCircuitBreaker(options, TimeProvider.System), logger);

        var failure = await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync(request, CancellationToken.None));

        Assert.Equal(status, failure.UpstreamStatusCode);
        var activity = Assert.Single(activities, item => Equals(item.GetTagItem("request.id"), request.RequestId));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(status, activity.GetTagItem("http.response.status_code"));
        Assert.Equal(failure.Code, activity.GetTagItem("error.code"));
        Assert.Contains(logger.Entries, item => item.Level == LogLevel.Error && item.Text.Contains(failure.Code, StringComparison.Ordinal));
        Assert.All(logger.Entries, item => Assert.Null(item.Exception));
        AssertSafe(activity, logger);
    }

    [Fact]
    public async Task OpenCircuit_RecordsRejectionWithoutAnotherHttpCallOrSensitiveMetricLabels()
    {
        var measurements = new ConcurrentBag<(string Name, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "MakanApp.AiService") meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => measurements.Add((instrument.Name, tags.ToArray())));
        listener.Start();
        var handler = new ResponseHandler(HttpStatusCode.ServiceUnavailable, ProviderSentinel);
        using var http = new HttpClient(handler);
        var options = new AiServiceOptions { Enabled = true, BaseUrl = "https://ai.example.test", CircuitBreakerFailureThreshold = 1 };
        var client = new AiServiceClient(http, new CredentialProvider(), options,
            new AiServiceCircuitBreaker(options, TimeProvider.System));

        await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync(Request(), CancellationToken.None));
        await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync(Request(), CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Contains(measurements, item => item.Name == "makan.ai.client.circuit.rejections");
        Assert.All(measurements.SelectMany(item => item.Tags), tag =>
        {
            Assert.Contains(tag.Key, new[] { "operation", "outcome", "error.code" });
            var value = Assert.IsType<string>(tag.Value);
            Assert.DoesNotContain(CredentialSentinel, value);
            Assert.DoesNotContain(PromptSentinel, value);
            Assert.DoesNotContain(ProviderSentinel, value);
        });
    }

    [Fact]
    public void Options_DoNotChooseAnEnvironmentAddressInCode() =>
        Assert.Empty(new AiServiceOptions().BaseUrl);

    private static ActivityListener Listen(ConcurrentBag<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "MakanApp.AiService",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static void AssertSafe(Activity activity, CaptureLogger logger)
    {
        var captured = string.Join('|', activity.TagObjects.Select(item => item.Key + "=" + item.Value)) +
            string.Join('|', logger.Entries.Select(item => item.Text)) + string.Join('|', logger.Scopes);
        foreach (var forbidden in new[] { CredentialSentinel, PromptSentinel, ProviderSentinel, "Authorization", "https://ai.example.test" })
            Assert.DoesNotContain(forbidden, captured, StringComparison.Ordinal);
    }

    private static AiServiceOptions Options() => new() { Enabled = true, BaseUrl = "https://ai.example.test" };
    private static AiChatRequest Request() => new(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), null,
        new AiActor("synthetic-user", "Student"), new AiScope(null, null, null, null),
        new AiMessage(PromptSentinel, "en"), AiAuthorizedContext.Empty);

    private sealed class CredentialProvider : IAiServiceCredentialProvider
    {
        public ValueTask<AiServiceCredential> GetCredentialAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AiServiceCredential("Bearer", CredentialSentinel, DateTime.UtcNow.AddMinutes(2)));
    }

    private sealed class ResponseHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CaptureLogger : ILogger<AiServiceClient>
    {
        public List<(LogLevel Level, string Text, Exception? Exception)> Entries { get; } = [];
        public List<string> Scopes { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                Scopes.Add(string.Join('|', fields.Select(item => item.Key + "=" + item.Value)));
            return null;
        }
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((level, formatter(state, exception), exception));
    }
}
