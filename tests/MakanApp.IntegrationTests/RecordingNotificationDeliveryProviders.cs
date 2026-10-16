using System.Collections.Concurrent;
using MakanApp.Application.Notification;

namespace MakanApp.IntegrationTests;

public sealed class NotificationDeliveryProviderTestControl
{
    private readonly ConcurrentQueue<PushNotificationDelivery> _pushDeliveries = new();
    private readonly ConcurrentQueue<NotificationSmsDelivery> _smsDeliveries = new();

    public DeliveryProviderResult PushResult { get; set; } = DeliveryProviderResult.Delivered("test-push");
    public DeliveryProviderResult SmsResult { get; set; } = DeliveryProviderResult.Delivered("test-sms");
    public bool PushWaitsForCancellation { get; set; }
    public bool SmsWaitsForCancellation { get; set; }
    public Func<PushNotificationDelivery, CancellationToken, Task>? OnPushSending { get; set; }
    public IReadOnlyCollection<PushNotificationDelivery> PushDeliveries => _pushDeliveries.ToArray();
    public IReadOnlyCollection<NotificationSmsDelivery> SmsDeliveries => _smsDeliveries.ToArray();

    public void Reset()
    {
        while (_pushDeliveries.TryDequeue(out _))
        {
        }

        while (_smsDeliveries.TryDequeue(out _))
        {
        }

        PushResult = DeliveryProviderResult.Delivered("test-push");
        SmsResult = DeliveryProviderResult.Delivered("test-sms");
        PushWaitsForCancellation = false;
        SmsWaitsForCancellation = false;
        OnPushSending = null;
    }

    public async Task<DeliveryProviderResult> SendPushAsync(
        PushNotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        _pushDeliveries.Enqueue(delivery);
        if (OnPushSending is not null) await OnPushSending(delivery, cancellationToken);
        if (PushWaitsForCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return PushResult;
    }

    public async Task<DeliveryProviderResult> SendSmsAsync(
        NotificationSmsDelivery delivery,
        CancellationToken cancellationToken)
    {
        _smsDeliveries.Enqueue(delivery);
        if (SmsWaitsForCancellation)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        return SmsResult;
    }
}

public sealed class RecordingPushNotificationProvider(
    NotificationDeliveryProviderTestControl control) : IPushNotificationProvider
{
    public Task<DeliveryProviderResult> SendAsync(
        PushNotificationDelivery delivery,
        CancellationToken cancellationToken) =>
        control.SendPushAsync(delivery, cancellationToken);
}

public sealed class RecordingNotificationSmsProvider(
    NotificationDeliveryProviderTestControl control) : INotificationSmsProvider
{
    public Task<DeliveryProviderResult> SendAsync(
        NotificationSmsDelivery delivery,
        CancellationToken cancellationToken) =>
        control.SendSmsAsync(delivery, cancellationToken);
}
