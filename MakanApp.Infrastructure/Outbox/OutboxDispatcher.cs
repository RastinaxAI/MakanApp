using System.Diagnostics;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Notification;
using MakanApp.Infrastructure.Hosting;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Outbox;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    WorkerTelemetry telemetry,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.IntegrationOutbox))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    processed = await scope.ServiceProvider
                        .GetRequiredService<OutboxProcessor>()
                        .DispatchBatchAsync(stoppingToken);
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException exception) when (exception.Number == 208)
                {
                    telemetry.RecordFailure(WorkerTelemetry.IntegrationOutbox, "Loop");
                    logger.LogDebug("جدول Outbox اعلان هنوز آماده نیست.");
                }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.IntegrationOutbox, "Loop");
                    logger.LogError("پردازش Outbox اعلان ناموفق بود. Code={FailureCode}", "OUTBOX_LOOP_FAILED");
                }
                finally
                {
                    telemetry.RecordBatch(WorkerTelemetry.IntegrationOutbox, processed, started);
                }
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
    WorkerTelemetry telemetry,
    ILogger<OutboxProcessor> logger)
{
    public const int BatchSize = 50;
    public const int MaximumAttempts = 10;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(30);
    private const string ProcessingErrorCode = "notification_processing_failed";
    public const string AttemptsExhaustedErrorCode = "notification_processing_exhausted";

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await RepairExhaustedClaimsAsync(cancellationToken);
        var processed = 0;
        while (processed < BatchSize)
        {
            var claim = await ClaimNextAsync(cancellationToken);
            if (claim is null) break;
            processed++;
            await ProcessOneAsync(claim.Id, claim.Token, cancellationToken);
        }

        return processed;
    }

    private async Task<int> RepairExhaustedClaimsAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        // فقط claim منقضی یا retry مصرف‌شده اصلاح می‌شود؛ رکورد نهایی دوباره وارد همان batch نمی‌شود.
        var repaired = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE TOP ({BatchSize}) [platform].[OutboxMessages] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
            SET [ClaimToken] = NULL, [ClaimedUntilUtc] = NULL, [NextAttemptAtUtc] = NULL,
                [LastErrorCode] = {AttemptsExhaustedErrorCode}
            WHERE [ProcessedAtUtc] IS NULL AND [AttemptCount] >= {MaximumAttempts}
              AND (([ClaimedUntilUtc] IS NOT NULL AND [ClaimedUntilUtc] <= {nowUtc})
                   OR ([ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NOT NULL))
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return repaired;
    }

    private async Task<OutboxClaim?> ClaimNextAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var pending = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT TOP (1) *
                FROM [platform].[OutboxMessages] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE [ProcessedAtUtc] IS NULL
                  AND [AttemptCount] < {MaximumAttempts}
                  AND ([ClaimedUntilUtc] IS NULL OR [ClaimedUntilUtc] <= {nowUtc})
                  AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc})
                ORDER BY [OccurredAtUtc], [Id]
                """)
            .ToArrayAsync(cancellationToken);
        OutboxClaim? claim = null;
        if (pending.Length != 0)
        {
            var message = pending[0];
            var token = Guid.NewGuid();
            var claimedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            message.Claim(token, claimedAtUtc, claimedAtUtc.Add(ClaimLease));
            claim = new OutboxClaim(message.Id, token);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return claim;
    }

    private async Task ProcessOneAsync(
        Guid messageId,
        Guid claimToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var message = await dbContext.OutboxMessages.FromSqlInterpolated($"""
                SELECT * FROM [platform].[OutboxMessages] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = {messageId} AND [ClaimToken] = {claimToken} AND [ProcessedAtUtc] IS NULL
                """).SingleOrDefaultAsync(cancellationToken);
            if (message is null || message.ClaimedUntilUtc is not DateTime claimedUntilUtc ||
                claimedUntilUtc <= timeProvider.GetUtcNow().UtcDateTime)
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
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            telemetry.RecordFailure(WorkerTelemetry.IntegrationOutbox, "Process");
            dbContext.ChangeTracker.Clear();
            using var completion = WorkerCompletionBudget.Create();
            await ScheduleRetryAsync(messageId, claimToken, completion.Token);
            if (await HasReachedAttemptLimitAsync(messageId, completion.Token))
            {
                logger.LogError(
                    "پردازش پیام اعلان {MessageId} پس از سقف تلاش متوقف شد. Code={FailureCode}",
                    messageId,
                    AttemptsExhaustedErrorCode);
            }
            else
            {
                logger.LogWarning(
                    "پردازش پیام اعلان {MessageId} ناموفق بود و دوباره تلاش می‌شود. Code={FailureCode}",
                    messageId,
                    ProcessingErrorCode);
            }
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var message = await dbContext.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM [platform].[OutboxMessages] WITH (UPDLOCK, HOLDLOCK)
            WHERE [Id] = {messageId} AND [ClaimToken] = {claimToken} AND [ProcessedAtUtc] IS NULL
            """).SingleOrDefaultAsync(cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (message is null || message.ClaimedUntilUtc is not DateTime claimedUntilUtc || claimedUntilUtc <= nowUtc)
        {
            return;
        }

        if (message.AttemptCount >= MaximumAttempts)
        {
            message.MarkFailed(claimToken, AttemptsExhaustedErrorCode);
        }
        else
        {
            var retrySeconds = Math.Min(300, Math.Max(2, message.AttemptCount * message.AttemptCount * 2));
            message.ScheduleRetry(claimToken, nowUtc.AddSeconds(retrySeconds), ProcessingErrorCode);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<bool> HasReachedAttemptLimitAsync(
        Guid messageId,
        CancellationToken cancellationToken) =>
        dbContext.OutboxMessages.AsNoTracking().AnyAsync(
            message => message.Id == messageId && message.AttemptCount >= MaximumAttempts,
            cancellationToken);

    private sealed record OutboxClaim(Guid Id, Guid Token);
}
