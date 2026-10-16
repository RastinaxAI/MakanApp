using System.Security.Cryptography;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed class WebPushNotificationProvider(
    HttpClient httpClient, WebPushTransportOptions options, PushDeviceOptions deviceOptions, TimeProvider timeProvider) : IPushNotificationProvider
{
    private readonly PushServiceClient _client = new(httpClient) { AutoRetryAfter = false };
    private readonly bool _configured = options.IsConfigured && deviceOptions.AllowedWebPushHosts.Count > 0;
    public bool IsConfigured => _configured;

    public async Task<DeliveryProviderResult> SendAsync(PushNotificationDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_configured) return options.HasConfiguration
            ? DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderConfigurationInvalid)
            : DeliveryProviderResult.Suppressed(NotificationDeliveryFailureReason.ProviderUnavailable);
        if (delivery.Platform != PushDevicePlatform.Web)
            return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderRejected);
        PushSubscription subscription;
        try
        {
            var endpoint = WebPushSubscriptionValidation.ValidateEndpoint(delivery.PushToken, deviceOptions.AllowedWebPushHosts);
            var keys = WebPushSubscriptionValidation.ValidateKeys(delivery.WebPushP256dh, delivery.WebPushAuth);
            subscription = new PushSubscription { Endpoint = endpoint.AbsoluteUri,
                Keys = new Dictionary<string, string> { ["p256dh"] = keys.P256dh, ["auth"] = keys.Auth } };
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.InvalidRegistration);
        }
        using var authentication = new VapidAuthentication(options.PublicKey, options.PrivateKey) { Subject = new Uri(options.Subject!).AbsoluteUri };
        var payload = PushWirePayload.Create(delivery);
        var message = new PushMessage(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web))
            { Topic = payload.Tag, TimeToLive = options.TimeToLiveSeconds };
        try
        {
            await _client.RequestPushMessageDeliveryAsync(subscription, message, authentication, cancellationToken);
            return DeliveryProviderResult.Delivered();
        }
        catch (PushServiceClientException exception)
        {
            return PushProviderHttpPolicy.Classify(exception.StatusCode, exception.Headers, timeProvider.GetUtcNow(), webPush: true);
        }
        catch (HttpRequestException)
        { return DeliveryProviderResult.Transient(NotificationDeliveryFailureReason.ProviderUnavailable); }
    }
}
