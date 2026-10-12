using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Notification;

public sealed class DevelopmentPushNotificationProvider(
    ILogger<DevelopmentPushNotificationProvider> logger) : IPushNotificationProvider
{
    public Task<DeliveryProviderResult> SendAsync(
        PushNotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "ارسال آزمایشی Push برای DeliveryAttempt {DeliveryAttemptId} ثبت شد.",
            delivery.DeliveryAttemptId);
        return Task.FromResult(DeliveryProviderResult.Delivered($"dev-push-{delivery.DeliveryAttemptId:N}"));
    }
}

public sealed class UnavailablePushNotificationProvider : IPushNotificationProvider
{
    public Task<DeliveryProviderResult> SendAsync(
        PushNotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DeliveryProviderResult.Suppressed(
            NotificationDeliveryFailureReason.ProviderUnavailable));
    }
}

public sealed class DevelopmentNotificationSmsProvider(
    ILogger<DevelopmentNotificationSmsProvider> logger) : INotificationSmsProvider
{
    public Task<DeliveryProviderResult> SendAsync(
        NotificationSmsDelivery delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "ارسال آزمایشی SMS عمومی با template {TemplateKey} برای DeliveryAttempt {DeliveryAttemptId} ثبت شد.",
            delivery.TemplateKey,
            delivery.DeliveryAttemptId);
        return Task.FromResult(DeliveryProviderResult.Delivered($"dev-sms-{delivery.DeliveryAttemptId:N}"));
    }
}

public sealed class UnavailableNotificationSmsProvider : INotificationSmsProvider
{
    public Task<DeliveryProviderResult> SendAsync(
        NotificationSmsDelivery delivery,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(DeliveryProviderResult.Suppressed(
            NotificationDeliveryFailureReason.ProviderUnavailable));
    }
}
