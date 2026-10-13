using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Outbox;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                processed = await scope.ServiceProvider
                    .GetRequiredService<OutboxProcessor>()
                    .DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SqlException exception) when (exception.Number == 208)
            {
                logger.LogDebug("جدول Outbox اعلان هنوز آماده نیست.");
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "پردازش Outbox اعلان ناموفق بود.");
            }

            if (processed < OutboxProcessor.BatchSize)
            {
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }
}

public sealed class OutboxProcessor(
    MakanDbContext dbContext,
    INotificationDeliveryCoordinator deliveryCoordinator,
    ISubscriptionActivationHandler subscriptionActivationHandler,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 50;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(30);
    private const string ProcessingErrorCode = "notification_processing_failed";

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var claims = await ClaimBatchAsync(cancellationToken);
        foreach (var claim in claims)
        {
            await ProcessOneAsync(claim.Id, claim.Token, cancellationToken);
        }

        return claims.Count;
    }

    private async Task<IReadOnlyCollection<OutboxClaim>> ClaimBatchAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var pending = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT TOP ({BatchSize}) *
                FROM [platform].[OutboxMessages] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [ProcessedAtUtc] IS NULL
                  AND ([ClaimedUntilUtc] IS NULL OR [ClaimedUntilUtc] <= {nowUtc})
                  AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc})
                ORDER BY [OccurredAtUtc], [Id]
                """)
            .ToArrayAsync(cancellationToken);
        var claims = pending.Select(message =>
        {
            var token = Guid.NewGuid();
            message.Claim(token, nowUtc, nowUtc.Add(ClaimLease));
            return new OutboxClaim(message.Id, token);
        }).ToArray();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return claims;
    }

    private async Task ProcessOneAsync(
        Guid messageId,
        Guid claimToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
                item => item.Id == messageId && item.ClaimToken == claimToken && item.ProcessedAtUtc == null,
                cancellationToken);
            if (message is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }

            if (SubscriptionActivationOutboxContract.Matches(message.MessageType))
            {
                var activation = JsonSerializer.Deserialize<SubscriptionActivationRequested>(message.Payload)
                    ?? throw new JsonException("payload فعال‌سازی اشتراک خالی است.");
                await subscriptionActivationHandler.HandleAsync(activation, cancellationToken);
            }
            else
            {
                var payload = JsonSerializer.Deserialize<NotificationOutboxPayload>(message.Payload)
                    ?? throw new JsonException("payload اعلان خالی است.");
                if (!NotificationOutboxContract.Matches(message.MessageType, payload.Type))
                {
                    throw new JsonException("قرارداد پیام Outbox اعلان معتبر نیست.");
                }

                var existingRecipients = await dbContext.Notifications
                    .Where(notification => notification.SourceEventId == payload.SourceEventId)
                    .Select(notification => notification.RecipientUserId)
                    .ToArrayAsync(cancellationToken);
                var existing = existingRecipients.ToHashSet();
                if (payload.Recipient.UserId != Guid.Empty && !existing.Contains(payload.Recipient.UserId))
                {
                    var notification = Domain.Notification.Notification.Create(
                        payload.SourceEventId,
                        payload.Recipient.UserId,
                        payload.OrganizationId,
                        payload.Category,
                        payload.Type,
                        payload.Recipient.Title,
                        payload.Recipient.Body,
                        payload.ResourceType,
                        payload.ResourceId,
                        payload.OccurredAtUtc);
                    dbContext.Notifications.Add(notification);
                    await deliveryCoordinator.ScheduleAsync(
                        notification,
                        payload.Recipient.SuppressExternalDelivery,
                        cancellationToken);
                }
            }

            message.MarkProcessed(claimToken, timeProvider.GetUtcNow().UtcDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            dbContext.ChangeTracker.Clear();
            await ScheduleRetryAsync(messageId, claimToken, CancellationToken.None);
            logger.LogWarning(exception, "پردازش پیام اعلان {MessageId} ناموفق بود و دوباره تلاش می‌شود.", messageId);
        }
        finally
        {
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task ScheduleRetryAsync(
        Guid messageId,
        Guid claimToken,
        CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages.SingleOrDefaultAsync(
            item => item.Id == messageId && item.ClaimToken == claimToken && item.ProcessedAtUtc == null,
            cancellationToken);
        if (message is null)
        {
            return;
        }

        var retrySeconds = Math.Min(300, Math.Max(2, message.AttemptCount * message.AttemptCount * 2));
        message.ScheduleRetry(
            claimToken,
            timeProvider.GetUtcNow().UtcDateTime.AddSeconds(retrySeconds),
            ProcessingErrorCode);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record OutboxClaim(Guid Id, Guid Token);
}
