using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed class MessengerPushNotificationProvider(WebPushNotificationProvider webPush, FcmPushNotificationProvider fcm,
    WebPushTransportOptions webOptions) : IPushNotificationProvider, IPushClientConfigurationProvider
{
    public string Availability => webPush.IsConfigured && fcm.IsConfigured ? "Configured" :
        webPush.IsConfigured || fcm.IsConfigured ? "PartiallyConfigured" : "Unavailable";

    public WebPushClientConfiguration GetWebPushConfiguration() => new(webPush.IsConfigured, webPush.IsConfigured ? webOptions.PublicKey : null);

    public Task<DeliveryProviderResult> SendAsync(PushNotificationDelivery delivery, CancellationToken cancellationToken) =>
        delivery.Platform == PushDevicePlatform.Web ? webPush.SendAsync(delivery, cancellationToken) : fcm.SendAsync(delivery, cancellationToken);
}
