using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2.Responses;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Notification;
using Xunit;

namespace MakanApp.UnitTests.Notification;

public sealed class PushTransportProviderTests
{
    [Fact]
    public async Task WebPushEncryptsPayloadAddsVapidAndStableTopicAndNeverUsesPrivatePreview()
    {
        using var handler = new CapturingHandler(HttpStatusCode.Created, "");
        using var client = new HttpClient(handler);
        var options = VapidOptions();
        Assert.True(options.IsConfigured);
        var provider = WebProvider(client, options);
        var delivery = WebDelivery();
        var result = await provider.SendAsync(delivery, default);
        Assert.Equal(DeliveryProviderOutcome.Delivered, result.Outcome);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("vapid", handler.Scheme);
        Assert.Equal(delivery.NotificationId.ToString("N"), handler.Topic);
        Assert.Equal("3600", handler.Ttl);
        Assert.Contains("aes128gcm", handler.ContentEncoding);
        Assert.DoesNotContain("private-body-never-send", Encoding.UTF8.GetString(handler.Body!));
        Assert.DoesNotContain("پیام جدید", Encoding.UTF8.GetString(handler.Body!));
        Assert.True(handler.Body!.Length > 86);
        Assert.DoesNotContain(options.PrivateKey!, handler.Authorization!);
        Assert.DoesNotContain(delivery.PushToken, delivery.ToString());
        Assert.DoesNotContain(delivery.WebPushAuth!, delivery.ToString());
        Assert.DoesNotContain(delivery.PushToken, new RegisterPushDeviceCommand(PushDevicePlatform.Web, delivery.PushToken).ToString());
        var command = new RegisterWebPushCommand(delivery.PushToken, delivery.WebPushP256dh!, delivery.WebPushAuth!);
        Assert.DoesNotContain(delivery.WebPushAuth!, command.ToString());
        Assert.DoesNotContain(delivery.PushToken, command.ToString());
    }

    [Theory]
    [InlineData(400, NotificationDeliveryFailureReason.ProviderConfigurationInvalid)]
    [InlineData(401, NotificationDeliveryFailureReason.ProviderAuthenticationFailed)]
    [InlineData(403, NotificationDeliveryFailureReason.ProviderAuthenticationFailed)]
    [InlineData(404, NotificationDeliveryFailureReason.InvalidRegistration)]
    [InlineData(410, NotificationDeliveryFailureReason.InvalidRegistration)]
    [InlineData(429, NotificationDeliveryFailureReason.ProviderRateLimited)]
    [InlineData(500, NotificationDeliveryFailureReason.ProviderUnavailable)]
    [InlineData(503, NotificationDeliveryFailureReason.ProviderUnavailable)]
    [InlineData(302, NotificationDeliveryFailureReason.ProviderConfigurationInvalid)]
    public async Task WebPushClassifiesFailureAndLeavesRetryToDurableWorker(int status, NotificationDeliveryFailureReason reason)
    {
        using var handler = new CapturingHandler((HttpStatusCode)status, "untrusted-provider-body") { RetryAfter = TimeSpan.FromSeconds(90) };
        using var client = new HttpClient(handler);
        var result = await WebProvider(client, VapidOptions()).SendAsync(WebDelivery(), default);
        Assert.Equal(reason, result.FailureReason);
        Assert.NotEqual(DeliveryProviderOutcome.Delivered, result.Outcome);
        Assert.Equal(1, handler.Calls);
        if (status is 429 or >= 500) Assert.Equal(TimeSpan.FromSeconds(90), result.RetryAfter);
    }

    [Fact]
    public async Task WebPushNeverReadsRawProviderErrorBody()
    {
        using var handler = new WebPushResponseSanitizingHandler { InnerHandler = new PoisonResponseHandler() };
        using var client = new HttpClient(handler);
        var result = await WebProvider(client, VapidOptions()).SendAsync(WebDelivery(), default);
        Assert.Equal(NotificationDeliveryFailureReason.InvalidRegistration, result.FailureReason);
    }

    [Fact]
    public async Task WebPushUnavailableOrInvalidConfigurationCannotReturnAccepted()
    {
        using var handler = new CapturingHandler(HttpStatusCode.Created, "");
        using var client = new HttpClient(handler);
        var unavailable = await WebProvider(client, new()).SendAsync(WebDelivery(), default);
        Assert.Equal(DeliveryProviderOutcome.Suppressed, unavailable.Outcome);
        Assert.Equal(NotificationDeliveryFailureReason.ProviderUnavailable, unavailable.FailureReason);
        var invalid = await WebProvider(client, new() { Subject = "mailto:ops@example.test", PublicKey = "bad", PrivateKey = "bad" }).SendAsync(WebDelivery(), default);
        Assert.Equal(NotificationDeliveryFailureReason.ProviderConfigurationInvalid, invalid.FailureReason);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void VapidPairMustMatchAndInvalidShapeIsSafelyUnavailable()
    {
        var first = VapidOptions(); var second = VapidOptions();
        Assert.False(new WebPushTransportOptions { Subject = first.Subject, PublicKey = first.PublicKey, PrivateKey = second.PrivateKey }.IsConfigured);
        Assert.False(new WebPushTransportOptions { Subject = "http://ops.example.test", PublicKey = first.PublicKey, PrivateKey = first.PrivateKey }.IsConfigured);
        Assert.False(new WebPushTransportOptions { Subject = "https://user:password@example.test", PublicKey = first.PublicKey, PrivateKey = first.PrivateKey }.IsConfigured);
        Assert.False(new WebPushTransportOptions { Subject = first.Subject, PublicKey = first.PublicKey, PrivateKey = "A" }.IsConfigured);
        Assert.False(new WebPushTransportOptions { Subject = first.Subject, PublicKey = first.PublicKey, PrivateKey = first.PrivateKey, TimeToLiveSeconds = -1 }.IsConfigured);
    }

    [Fact]
    public async Task WebPushRevalidatesEndpointAndSubscriptionBeforeNetwork()
    {
        using var handler = new CapturingHandler(HttpStatusCode.Created, ""); using var client = new HttpClient(handler);
        var provider = WebProvider(client, VapidOptions());
        foreach (var delivery in new[] { WebDelivery() with { PushToken = "https://127.0.0.1/token" }, WebDelivery() with { WebPushAuth = null } })
            Assert.Equal(NotificationDeliveryFailureReason.InvalidRegistration, (await provider.SendAsync(delivery, default)).FailureReason);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(PushDevicePlatform.Android)]
    [InlineData(PushDevicePlatform.Ios)]
    public async Task FcmHttpV1SendsMinimalPayloadWithOAuthAndStableDedupeTag(PushDevicePlatform platform)
    {
        using var handler = new CapturingHandler(HttpStatusCode.OK, "{\"name\":\"projects/makan-test/messages/accepted\"}");
        using var client = new HttpClient(handler);
        var delivery = Delivery(platform);
        var result = await FcmProvider(client).SendAsync(delivery, default);
        Assert.Equal(DeliveryProviderOutcome.Delivered, result.Outcome);
        Assert.Equal("Bearer", handler.Scheme);
        Assert.Equal("https://fcm.googleapis.com/v1/projects/makan-test/messages:send", handler.Uri!.AbsoluteUri);
        using var json = JsonDocument.Parse(handler.Body!);
        var message = json.RootElement.GetProperty("message");
        Assert.Equal(delivery.PushToken, message.GetProperty("token").GetString());
        Assert.Equal(delivery.NotificationId.ToString("N"), message.GetProperty("android").GetProperty("notification").GetProperty("tag").GetString());
        Assert.Equal(delivery.NotificationId.ToString("N"), message.GetProperty("apns").GetProperty("headers").GetProperty("apns-collapse-id").GetString());
        Assert.Equal($"makan://conversations/{delivery.ConversationId:D}", message.GetProperty("data").GetProperty("deepLink").GetString());
        var payload = Encoding.UTF8.GetString(handler.Body!);
        Assert.DoesNotContain("private-body-never-send", payload);
        Assert.DoesNotContain("private-title-never-send", payload);
        Assert.DoesNotContain("ageClassification", payload);
        Assert.DoesNotContain("guardian", payload);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(404, "UNREGISTERED", NotificationDeliveryFailureReason.InvalidRegistration)]
    [InlineData(400, "INVALID_ARGUMENT", NotificationDeliveryFailureReason.InvalidRegistration)]
    [InlineData(403, "SENDER_ID_MISMATCH", NotificationDeliveryFailureReason.ProviderAuthenticationFailed)]
    [InlineData(401, "UNAUTHENTICATED", NotificationDeliveryFailureReason.ProviderAuthenticationFailed)]
    [InlineData(429, "QUOTA_EXCEEDED", NotificationDeliveryFailureReason.ProviderRateLimited)]
    [InlineData(500, "INTERNAL", NotificationDeliveryFailureReason.ProviderUnavailable)]
    [InlineData(503, "UNAVAILABLE", NotificationDeliveryFailureReason.ProviderUnavailable)]
    public async Task FcmDistinguishesInvalidTokenFromAuthRateLimitAndTransientFailure(int status, string error, NotificationDeliveryFailureReason reason)
    {
        var body = JsonSerializer.Serialize(new { error = new { status = error, details = new[] {
            new Dictionary<string, string> { ["@type"] = "type.googleapis.com/google.firebase.fcm.v1.FcmError", ["errorCode"] = error } } } });
        using var handler = new CapturingHandler((HttpStatusCode)status, body) { RetryAfter = TimeSpan.FromMinutes(2) };
        using var client = new HttpClient(handler);
        var result = await FcmProvider(client).SendAsync(Delivery(), default);
        Assert.Equal(reason, result.FailureReason);
        if (status is 429 or >= 500) Assert.Equal(TimeSpan.FromMinutes(2), result.RetryAfter);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(400, "{\"error\":{\"status\":\"INVALID_ARGUMENT\",\"details\":[{\"@type\":\"type.googleapis.com/google.rpc.BadRequest\"}]}}")]
    [InlineData(404, "{\"error\":{\"status\":\"NOT_FOUND\"}}")]
    [InlineData(200, "{}")]
    [InlineData(200, "{\"name\":\"not-a-provider-message\"}")]
    public async Task FcmDoesNotRevokeTokenForGenericPayloadOrProjectErrorsOrAcceptMissingReceipt(int status, string body)
    {
        using var handler = new CapturingHandler((HttpStatusCode)status, body); using var client = new HttpClient(handler);
        var result = await FcmProvider(client).SendAsync(Delivery(), default);
        Assert.NotEqual(DeliveryProviderOutcome.Delivered, result.Outcome);
        Assert.NotEqual(NotificationDeliveryFailureReason.InvalidRegistration, result.FailureReason);
    }

    [Fact]
    public async Task FcmMissingConfigurationAndCancelledRequestsNeverProduceAcceptedResult()
    {
        using var handler = new CapturingHandler(HttpStatusCode.OK, "{}"); using var client = new HttpClient(handler);
        var missing = new FcmPushNotificationProvider(client, new FakeTokenProvider(false), new(), TimeProvider.System);
        Assert.Equal(NotificationDeliveryFailureReason.ProviderUnavailable, (await missing.SendAsync(Delivery(), default)).FailureReason);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FcmProvider(client).SendAsync(Delivery(), cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WebProvider(client, VapidOptions()).SendAsync(WebDelivery(), cancelled.Token));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ExcessiveRetryAfterAndResponseBodyAreBoundedAndNeverAccepted()
    {
        using var handler = new CapturingHandler(HttpStatusCode.TooManyRequests, "") { RetryAfter = TimeSpan.FromDays(2) };
        using var client = new HttpClient(handler);
        var result = await FcmProvider(client).SendAsync(Delivery(), default);
        Assert.Equal(DeliveryProviderOutcome.PermanentFailure, result.Outcome);
        Assert.Equal(NotificationDeliveryFailureReason.ProviderRejected, result.FailureReason);
        using var large = new StringContent(new string('x', 16_385));
        await Assert.ThrowsAsync<InvalidDataException>(() => PushProviderHttpPolicy.ReadBoundedAsync(large, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringProviderIoIsPropagatedWithoutFakeAcceptance(bool web)
    {
        using var handler = new WaitingHandler(); using var client = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => web
            ? WebProvider(client, VapidOptions()).SendAsync(WebDelivery(), cancellation.Token)
            : FcmProvider(client).SendAsync(Delivery(), cancellation.Token));
    }

    [Theory]
    [InlineData(401, NotificationDeliveryFailureReason.ProviderAuthenticationFailed)]
    [InlineData(408, NotificationDeliveryFailureReason.ProviderUnavailable)]
    [InlineData(429, NotificationDeliveryFailureReason.ProviderRateLimited)]
    [InlineData(502, NotificationDeliveryFailureReason.ProviderUnavailable)]
    [InlineData(504, NotificationDeliveryFailureReason.ProviderUnavailable)]
    [InlineData(503, NotificationDeliveryFailureReason.ProviderUnavailable)]
    public async Task OAuthFailureIsClassifiedWithoutSendingOrRevokingDevice(int status, NotificationDeliveryFailureReason reason)
    {
        using var handler = new CapturingHandler(HttpStatusCode.OK, "{}"); using var client = new HttpClient(handler);
        var provider = new FcmPushNotificationProvider(client, new FailingTokenProvider((HttpStatusCode)status),
            new() { ProjectId = "makan-test" }, TimeProvider.System);
        Assert.Equal(reason, (await provider.SendAsync(Delivery(), default)).FailureReason);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task MalformedFcmProviderResponseIsTransientNotAcceptedOrInvalidToken()
    {
        using var handler = new CapturingHandler(HttpStatusCode.OK, "not-json"); using var client = new HttpClient(handler);
        var result = await FcmProvider(client).SendAsync(Delivery(), default);
        Assert.Equal(DeliveryProviderOutcome.TransientFailure, result.Outcome);
        Assert.Equal(NotificationDeliveryFailureReason.ProviderUnavailable, result.FailureReason);
    }

    private static WebPushNotificationProvider WebProvider(HttpClient client, WebPushTransportOptions options) =>
        new(client, options, new PushDeviceOptions { AllowedWebPushHosts = ["push.example.test"] }, TimeProvider.System);
    private static FcmPushNotificationProvider FcmProvider(HttpClient client) =>
        new(client, new FakeTokenProvider(true), new FcmTransportOptions { ProjectId = "makan-test" }, TimeProvider.System);
    private static PushNotificationDelivery Delivery(PushDevicePlatform platform = PushDevicePlatform.Android) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "ephemeral-fcm-test-registration", "private-title-never-send", "private-body-never-send", "untrusted-deep-link",
            platform, Type: NotificationType.MessageReceived, ConversationId: Guid.NewGuid());
    private static PushNotificationDelivery WebDelivery()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256); var point = key.ExportParameters(false).Q;
        return Delivery(PushDevicePlatform.Web) with { PushToken = "https://push.example.test/subscription",
            WebPushP256dh = Encode(new byte[] { 4 }.Concat(point.X!).Concat(point.Y!).ToArray()), WebPushAuth = Encode(RandomNumberGenerator.GetBytes(16)) };
    }
    private static WebPushTransportOptions VapidOptions()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var parameters = key.ExportParameters(true);
        return new() { Subject = "MAILTO:ops@example.test", PublicKey = Encode(new byte[] { 4 }.Concat(parameters.Q.X!).Concat(parameters.Q.Y!).ToArray()), PrivateKey = Encode(parameters.D!) };
    }
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private sealed class FakeTokenProvider(bool configured) : IFcmAccessTokenProvider
    {
        public bool IsConfigured => configured;
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("ephemeral-test-oauth-access-token");
    }
    private sealed class FailingTokenProvider(HttpStatusCode status) : IFcmAccessTokenProvider
    {
        public bool IsConfigured => true;
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) =>
            throw new TokenResponseException(new TokenErrorResponse { Error = "test-oauth-error" }, status);
    }
    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("مسیر پس از cancellation نباید اجرا شود.");
        }
    }
    private sealed class CapturingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public byte[]? Body { get; private set; }
        public string? Scheme { get; private set; }
        public string? Authorization { get; private set; }
        public string? Topic { get; private set; }
        public string? Ttl { get; private set; }
        public Uri? Uri { get; private set; }
        public string ContentEncoding { get; private set; } = "";
        public TimeSpan? RetryAfter { get; init; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Uri = request.RequestUri; Scheme = request.Headers.Authorization?.Scheme; Authorization = request.Headers.Authorization?.Parameter;
            Topic = request.Headers.TryGetValues("Topic", out var topics) ? topics.Single() : null;
            Ttl = request.Headers.TryGetValues("TTL", out var ttls) ? ttls.Single() : null;
            ContentEncoding = string.Join(',', request.Content?.Headers.ContentEncoding ?? []);
            var response = new HttpResponseMessage(status) { Content = new StringContent(responseBody) };
            if (RetryAfter.HasValue) response.Headers.RetryAfter = new RetryConditionHeaderValue(RetryAfter.Value);
            return response;
        }
    }
    private sealed class PoisonResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Gone) { Content = new PoisonContent() });
    }
    private sealed class PoisonContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new InvalidOperationException("بدنه نباید خوانده شود.");
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
