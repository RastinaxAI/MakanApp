using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreateFailedNotificationDeliveryAttemptAsync(
        Guid recipientUserId,
        string destinationReference,
        string title,
        string body)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = Clock.GetUtcNow().UtcDateTime;
        var notification = Notification.Create(
            Guid.NewGuid(),
            recipientUserId,
            null,
            NotificationCategory.AccountSystem,
            NotificationType.MessageReceived,
            title,
            body,
            NotificationResourceType.Conversation,
            Guid.NewGuid(),
            nowUtc);
        var attempt = NotificationDeliveryAttempt.Schedule(
            notification.Id,
            NotificationDeliveryChannel.Sms,
            destinationReference,
            nowUtc,
            nowUtc);
        var claimToken = Guid.NewGuid();
        attempt.Claim(claimToken, nowUtc, nowUtc.AddMinutes(1));
        attempt.MarkPermanentFailure(
            claimToken,
            NotificationDeliveryFailureReason.ProviderRejected);

        dbContext.Notifications.Add(notification);
        dbContext.NotificationDeliveryAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();
        return attempt.Id;
    }

    public async Task<NotificationDeliveryStatus> GetNotificationDeliveryStatusAsync(
        Guid deliveryAttemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.NotificationDeliveryAttempts.AsNoTracking()
            .Where(item => item.Id == deliveryAttemptId)
            .Select(item => item.Status)
            .SingleAsync();
    }
}
