using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(PushDevicePlatform.Web)]
    [InlineData(PushDevicePlatform.Android)]
    [InlineData(PushDevicePlatform.Ios)]
    public async Task SqlWorkerUsesPersistedRegistrationAndRealAdapterWithControlledHttp(PushDevicePlatform platform)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var subscription = P1WebSubscription();
        var mobileToken = $"p1-mobile-ephemeral-{Guid.NewGuid():N}";
        using var registration = platform == PushDevicePlatform.Web
            ? await setup.SecondClient.PostAsJsonAsync("/api/v1/notifications/devices/web-push", subscription, JsonOptions)
            : await setup.SecondClient.PostAsJsonAsync("/api/v1/notifications/devices", new RegisterPushDeviceCommand(platform, mobileToken), JsonOptions);
        registration.EnsureSuccessStatusCode();
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "P1-PRIVATE-NOTIFICATION-CONTENT");
        await factory.DrainNotificationOutboxAsync();
        var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
        var web = new P1TransportHandler(true); var fcm = new P1TransportHandler(false);
        var options = CreateP1VapidOptions(); var logs = new SensitiveLogTestControl();
        await using var services = CreateP1TransportServices(web, fcm, options, logs);
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<NotificationDeliveryProcessor>().DispatchBatchAsync(default);
        Assert.Equal(NotificationDeliveryStatus.Delivered, (await P1PushAttemptAsync(notificationId)).Status);
        var calls = platform == PushDevicePlatform.Web ? web.Calls : fcm.Calls;
        var call = Assert.Single(calls, call => call.NotificationId == notificationId);
        Assert.DoesNotContain("P1-PRIVATE-NOTIFICATION-CONTENT", call.Body);
        if (platform == PushDevicePlatform.Web) Assert.Equal("aes128gcm", call.ContentEncoding);
        else
        {
            using var json = JsonDocument.Parse(call.Body);
            Assert.Equal(setup.ConversationId.ToString("D"), json.RootElement.GetProperty("message").GetProperty("data").GetProperty("conversationId").GetString());
        }
        var capturedLogs = string.Join('\n', logs.Snapshot());
        foreach (var secret in new[] { subscription.Endpoint, subscription.Auth, subscription.P256dh, mobileToken, options.PrivateKey! })
            Assert.DoesNotContain(secret, capturedLogs);
    }

    [Theory]
    [InlineData(PushDevicePlatform.Web, 410, true, false)]
    [InlineData(PushDevicePlatform.Android, 404, true, false)]
    [InlineData(PushDevicePlatform.Ios, 403, false, false)]
    [InlineData(PushDevicePlatform.Android, 429, false, true)]
    public async Task RealAdapterResultControlsSqlRetryOrRevocationWithoutFakeDelivery(PushDevicePlatform platform, int status, bool revoke, bool retry)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var registered = platform == PushDevicePlatform.Web
            ? await setup.SecondClient.PostAsJsonAsync("/api/v1/notifications/devices/web-push", P1WebSubscription(), JsonOptions)
            : await setup.SecondClient.PostAsJsonAsync("/api/v1/notifications/devices", new RegisterPushDeviceCommand(platform, $"p1-invalid-{Guid.NewGuid():N}"), JsonOptions);
        registered.EnsureSuccessStatusCode();
        var device = (await registered.Content.ReadFromJsonAsync<PushDeviceRegistrationResult>(JsonOptions))!;
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-adapter-result");
        await factory.DrainNotificationOutboxAsync();
        var notificationId = await factory.GetNotificationIdAsync(sent.MessageId, setup.Second.UserId);
        var web = new P1TransportHandler(true) { Status = (HttpStatusCode)status };
        var fcm = new P1TransportHandler(false) { Status = (HttpStatusCode)status };
        await using var services = CreateP1TransportServices(web, fcm, CreateP1VapidOptions(), new());
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<NotificationDeliveryProcessor>().DispatchBatchAsync(default);
        var attempt = await P1PushAttemptAsync(notificationId);
        Assert.Equal(retry ? NotificationDeliveryStatus.Deferred : NotificationDeliveryStatus.Failed, attempt.Status);
        if (retry) Assert.True(attempt.NextAttemptAtUtc >= DateTime.UtcNow.AddSeconds(80));
        await using var check = factory.Services.CreateAsyncScope();
        var stored = await check.ServiceProvider.GetRequiredService<MakanDbContext>().PushDeviceRegistrations.SingleAsync(d => d.Id == device.Id);
        Assert.Equal(revoke, stored.RevokedAtUtc.HasValue);
        Assert.Single((platform == PushDevicePlatform.Web ? web : fcm).Calls, c => c.NotificationId == notificationId);
        if (!revoke) (await setup.SecondClient.DeleteAsync($"/api/v1/notifications/devices/{device.Id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PublicWebPushConfigurationIsAuthenticatedAndCannotExposePrivateKey()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/notifications/devices/web-push/configuration")).StatusCode);
        using var setup = await CreateAdvancedConversationSetupAsync();
        var options = CreateP1VapidOptions();
        await using var services = CreateP1TransportServices(new(true), new(false), options, new());
        Assert.IsType<MessengerPushNotificationProvider>(services.GetRequiredService<IPushNotificationProvider>());
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(registrations =>
        {
            registrations.RemoveAll<IPushClientConfigurationProvider>();
            registrations.AddSingleton(services.GetRequiredService<IPushClientConfigurationProvider>());
        }));
        using var client = host.CreateClient();
        await AuthenticateP1SessionAsync(client, setup.First.PhoneNumber, host.Services);
        using var response = await client.GetAsync("/api/v1/notifications/devices/web-push/configuration");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();
        var config = JsonSerializer.Deserialize<WebPushClientConfiguration>(raw, JsonOptions)!;
        Assert.True(config.IsConfigured); Assert.Equal(options.PublicKey, config.VapidPublicKey);
        Assert.DoesNotContain(options.PrivateKey!, raw);
        Assert.DoesNotContain("ServiceAccountPath", raw);
        using var openApi = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.True(openApi.RootElement.GetProperty("paths").TryGetProperty("/api/v1/notifications/devices/web-push/configuration", out _));
        var properties = openApi.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(nameof(WebPushClientConfiguration)).GetProperty("properties");
        Assert.True(properties.TryGetProperty("vapidPublicKey", out _)); Assert.False(properties.TryGetProperty("privateKey", out _));
    }

    [Fact]
    public async Task MissingProviderConfigurationRemainsUnavailableWithoutAcceptingPush()
    {
        var configuration = new ConfigurationBuilder().AddConfiguration(factory.Services.GetRequiredService<IConfiguration>())
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:PushProvider"] = "WebPushAndFcm", ["Notifications:WebPush:VapidSubject"] = null,
                ["Notifications:WebPush:VapidPublicKey"] = null, ["Notifications:WebPush:VapidPrivateKey"] = null,
                ["Notifications:Fcm:ProjectId"] = null, ["Notifications:Fcm:ServiceAccountPath"] = null,
                ["GOOGLE_APPLICATION_CREDENTIALS"] = null
            }).Build();
        var registrations = new ServiceCollection(); registrations.AddLogging(); registrations.AddInfrastructure(configuration, "Testing");
        await using var provider = registrations.BuildServiceProvider();
        var transport = Assert.IsType<MessengerPushNotificationProvider>(provider.GetRequiredService<IPushNotificationProvider>());
        Assert.Equal("Unavailable", transport.Availability);
        Assert.False(provider.GetRequiredService<IPushClientConfigurationProvider>().GetWebPushConfiguration().IsConfigured);
        var result = await transport.SendAsync(new(Guid.NewGuid(), Guid.NewGuid(), "not-a-real-device-token", "", "", ""), default);
        Assert.Equal(DeliveryProviderOutcome.Suppressed, result.Outcome);
    }

    [Theory]
    [InlineData("external_account", "makan-test", "https://oauth2.googleapis.com/token")]
    [InlineData("service_account", "other-project", "https://oauth2.googleapis.com/token")]
    [InlineData("service_account", "makan-test", "https://unapproved.example.test/token")]
    public async Task GoogleCredentialCannotSelectAnotherCredentialTypeProjectOrTokenServer(string type, string project, string tokenUri)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { type, project_id = project, token_uri = tokenUri }));
            using var provider = new GoogleFcmAccessTokenProvider(new() { ProjectId = "makan-test", ServiceAccountPath = path });
            await Assert.ThrowsAsync<InvalidDataException>(() => provider.GetAccessTokenAsync(default));
        }
        finally { File.Delete(path); }
    }

    private ServiceProvider CreateP1TransportServices(P1TransportHandler web, P1TransportHandler fcm, WebPushTransportOptions options, SensitiveLogTestControl logs)
    {
        var configuration = new ConfigurationBuilder().AddConfiguration(factory.Services.GetRequiredService<IConfiguration>())
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:PushProvider"] = "WebPushAndFcm", ["Notifications:WebPush:VapidSubject"] = options.Subject,
                ["Notifications:WebPush:VapidPublicKey"] = options.PublicKey, ["Notifications:WebPush:VapidPrivateKey"] = options.PrivateKey,
                ["Notifications:WebPush:AllowedEndpointHosts:0"] = "push.example.test", ["Notifications:Fcm:ProjectId"] = "makan-test"
            }).Build();
        var registrations = new ServiceCollection();
        registrations.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(new RecordingTestLoggerProvider(logs)));
        registrations.AddInfrastructure(configuration, "Testing");
        registrations.RemoveAll<TimeProvider>(); registrations.AddSingleton<TimeProvider>(factory.Clock);
        registrations.RemoveAll<IFcmAccessTokenProvider>(); registrations.AddSingleton<IFcmAccessTokenProvider, P1FakeGoogleTokenProvider>();
        registrations.AddHttpClient("notification-web-push").ConfigurePrimaryHttpMessageHandler(() =>
            new WebPushResponseSanitizingHandler { InnerHandler = web });
        registrations.AddHttpClient("notification-fcm").ConfigurePrimaryHttpMessageHandler(() => fcm);
        return registrations.BuildServiceProvider();
    }

    private static WebPushTransportOptions CreateP1VapidOptions()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256); var parameters = key.ExportParameters(true);
        static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new() { Subject = "mailto:ops@example.test", PublicKey = Encode(new byte[] { 4 }.Concat(parameters.Q.X!).Concat(parameters.Q.Y!).ToArray()), PrivateKey = Encode(parameters.D!) };
    }
    private sealed class P1FakeGoogleTokenProvider : IFcmAccessTokenProvider
    {
        public bool IsConfigured => true;
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("p1-ephemeral-test-oauth");
    }
    private sealed record P1TransportCall(Guid NotificationId, string Body, string ContentEncoding);
    private sealed class P1TransportHandler(bool web) : HttpMessageHandler
    {
        public HttpStatusCode? Status { get; init; }
        public ConcurrentQueue<P1TransportCall> Calls { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Guid notificationId;
            if (web) notificationId = Guid.ParseExact(request.Headers.GetValues("Topic").Single(), "N");
            else
            {
                using var json = JsonDocument.Parse(body);
                notificationId = Guid.Parse(json.RootElement.GetProperty("message").GetProperty("data").GetProperty("notificationId").GetString()!);
            }
            Calls.Enqueue(new(notificationId, body, string.Join(',', request.Content.Headers.ContentEncoding)));
            var status = Status ?? (web ? HttpStatusCode.Created : HttpStatusCode.OK);
            var content = status == HttpStatusCode.NotFound
                ? "{\"error\":{\"details\":[{\"@type\":\"type.googleapis.com/google.firebase.fcm.v1.FcmError\",\"errorCode\":\"UNREGISTERED\"}]}}"
                : "{\"name\":\"projects/makan-test/messages/controlled-http\"}";
            var response = new HttpResponseMessage(status) { Content = new StringContent(content) };
            if (status == HttpStatusCode.TooManyRequests) response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
            return response;
        }
    }
}
