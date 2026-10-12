using MakanApp.Application.Notification;
using MakanApp.Application.Assessment;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainNotification = MakanApp.Domain.Notification.Notification;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreateNotificationAsync(
        Guid userId,
        Guid? organizationId,
        DateTime createdAtUtc,
        bool read = false,
        NotificationCategory category = NotificationCategory.Educational)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var notification = DomainNotification.Create(
            Guid.NewGuid(),
            userId,
            organizationId,
            category,
            NotificationType.AssignmentPublished,
            "تکلیف جدید منتشر شد",
            "یک تکلیف جدید برای شما منتشر شده است.",
            NotificationResourceType.Assignment,
            Guid.NewGuid(),
            createdAtUtc);
        if (read)
        {
            notification.MarkRead(createdAtUtc.AddMinutes(1));
        }

        dbContext.Notifications.Add(notification);
        await dbContext.SaveChangesAsync();
        return notification.Id;
    }

    public async Task<Guid> CreateOrganizationPersonForUserAsync(
        Guid organizationId,
        Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var personId = await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => user.PersonId)
            .SingleAsync() ?? throw new InvalidOperationException("کاربر تست پروفایل کامل ندارد.");
        var organizationPerson = MakanApp.Domain.Organization.OrganizationPerson.CreateActive(
            organizationId,
            personId,
            DateTime.UtcNow);
        dbContext.OrganizationPersons.Add(organizationPerson);
        await dbContext.SaveChangesAsync();
        return organizationPerson.Id;
    }

    public async Task<IReadOnlyCollection<Guid>> EnqueueNotificationAsync(NotificationIntent intent)
    {
        await using var scope = Services.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var envelopes = NotificationOutboxContract.Create(intent);
        outbox.EnqueueRange(envelopes);
        await dbContext.SaveChangesAsync();
        return envelopes.Select(envelope => envelope.MessageId).ToArray();
    }

    public async Task<IReadOnlyCollection<Guid>> ResolveActiveGuardianUserIdsAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime nowUtc)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEvaluationStore>()
            .GetActiveGuardianUserIdsAsync(
                organizationId,
                learnerOrganizationPersonId,
                nowUtc,
                CancellationToken.None);
    }

    public async Task AddMalformedNotificationOutboxAsync(Guid eventId, DateTime occurredAtUtc)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.OutboxMessages.Add(OutboxMessage.Create(
            eventId,
            "notification.assignment-published.v1",
            "{malformed",
            occurredAtUtc));
        await dbContext.SaveChangesAsync();
    }

    public async Task RepairNotificationOutboxAsync(Guid eventId, NotificationIntent intent)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var payload = NotificationOutboxContract.Create(intent).Single().Payload;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE [platform].[OutboxMessages]
            SET [Payload] = {payload}, [NextAttemptAtUtc] = NULL
            WHERE [Id] = {eventId}
            """);
    }

    public async Task<int> ProcessNotificationOutboxAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<OutboxProcessor>()
            .DispatchBatchAsync(CancellationToken.None);
    }

    public async Task<int> DrainNotificationOutboxAsync()
    {
        var total = 0;
        for (var batch = 0; batch < 100; batch++)
        {
            var processed = await ProcessNotificationOutboxAsync();
            total += processed;
            if (processed < OutboxProcessor.BatchSize)
            {
                return total;
            }
        }

        throw new InvalidOperationException("Backlog اعلان تست در محدوده مجاز تخلیه نشد.");
    }

    public async Task<int[]> ProcessNotificationOutboxConcurrentlyAsync()
    {
        var first = ProcessNotificationOutboxAsync();
        var second = ProcessNotificationOutboxAsync();
        return await Task.WhenAll(first, second);
    }

    public async Task<(int AttemptCount, bool Processed, bool RetryScheduled, string? ErrorCode)>
        GetNotificationOutboxStateAsync(Guid eventId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.Id == eventId)
            .Select(message => new ValueTuple<int, bool, bool, string?>(
                message.AttemptCount,
                message.ProcessedAtUtc != null,
                message.NextAttemptAtUtc != null,
                message.LastErrorCode))
            .SingleAsync();
    }

    public async Task<int> CountNotificationsAsync(Guid sourceEventId, Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Notifications.CountAsync(notification =>
            notification.SourceEventId == sourceEventId &&
            notification.RecipientUserId == userId);
    }

    public async Task<int> CountPendingNotificationOutboxAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.OutboxMessages
            .AsNoTracking()
            .CountAsync(message => message.ProcessedAtUtc == null);
    }

    public async Task<int> CountOutboxMessagesAsync(string messageType)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.OutboxMessages
            .AsNoTracking()
            .CountAsync(message => message.MessageType == messageType);
    }
}
