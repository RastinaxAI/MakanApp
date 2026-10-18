using System.Diagnostics;
using MakanApp.Application.Messaging;
using MakanApp.Infrastructure.Hosting;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingRealtimeOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    MessagingOutboxWakeSignal wakeSignal,
    WorkerTelemetry telemetry,
    ILogger<MessagingRealtimeOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.MessagingRealtimeOutbox))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    processed = await scope.ServiceProvider
                        .GetRequiredService<MessagingRealtimeOutboxProcessor>()
                        .DispatchBatchAsync(stoppingToken);
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException exception) when (exception.Number == 208)
                {
                    telemetry.RecordFailure(WorkerTelemetry.MessagingRealtimeOutbox, "Loop");
                    logger.LogError("ساختار لازم برای Outbox پیام‌رسانی آماده نیست. Code={FailureCode}", "REALTIME_OUTBOX_SCHEMA_UNAVAILABLE");
                }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.MessagingRealtimeOutbox, "Loop");
                    logger.LogError("پردازش Outbox پیام‌رسانی ناموفق بود. Code={FailureCode}", "REALTIME_OUTBOX_LOOP_FAILED");
                }
                finally
                {
                    telemetry.RecordBatch(WorkerTelemetry.MessagingRealtimeOutbox, processed, started);
                }
            }

            if (processed < MessagingRealtimeOutboxProcessor.BatchSize)
            {
                await wakeSignal.WaitAsync(IdleDelay, stoppingToken);
            }
        }
    }
}

public sealed class MessagingRealtimeOutboxProcessor(
    MakanDbContext dbContext,
    IMessagingRealtimeNotifier notifier,
    TimeProvider timeProvider,
    WorkerTelemetry telemetry,
    ILogger<MessagingRealtimeOutboxProcessor> logger)
{
    public const int BatchSize = 50;
    public const int MaximumAttempts = 10;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(10);

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        await RepairExhaustedClaimsAsync(cancellationToken);
        var processed = 0;
        while (processed < BatchSize)
        {
            var claim = await ClaimNextAsync(cancellationToken);
            if (claim is null) break;
            processed++;
            if (!await DispatchOneAsync(claim, cancellationToken)) break;
        }
        return processed;
    }

    private async Task<int> RepairExhaustedClaimsAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var repaired = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE TOP ({BatchSize}) [messaging].[RealtimeOutbox] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
            SET [ClaimedUntilUtc] = NULL, [NextAttemptAtUtc] = NULL
            WHERE [DispatchedAtUtc] IS NULL AND [AttemptCount] >= {MaximumAttempts}
              AND (([ClaimedUntilUtc] IS NOT NULL AND [ClaimedUntilUtc] <= {nowUtc})
                   OR ([ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NOT NULL))
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (repaired > 0)
        {
            telemetry.RecordOutcome(WorkerTelemetry.MessagingRealtimeOutbox, "Failed", repaired);
            logger.LogError("کارهای رهاشده realtime پس از سقف تلاش نهایی شدند. Count={Count}; Code={FailureCode}",
                repaired, "REALTIME_OUTBOX_ATTEMPTS_EXHAUSTED");
        }
        return repaired;
    }

    private async Task<RealtimeClaim?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var pending = await dbContext.MessagingRealtimeOutboxMessages
            .FromSqlInterpolated($"""
                SELECT TOP (1) *
                FROM [messaging].[RealtimeOutbox] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE [DispatchedAtUtc] IS NULL
                  AND [AttemptCount] < {MaximumAttempts}
                  AND ([ClaimedUntilUtc] IS NULL OR [ClaimedUntilUtc] <= {nowUtc})
                  AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc})
                ORDER BY [OccurredAtUtc], [Id]
                """)
            .ToArrayAsync(cancellationToken);
        var message = pending.FirstOrDefault();
        var claimedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var recovered = message?.ClaimedUntilUtc is DateTime previousLease && previousLease <= claimedAtUtc;
        message?.Claim(claimedAtUtc, claimedAtUtc.Add(ClaimLease));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var claim = message is null ? null : new RealtimeClaim(message.Id, message.ChangeEventId, message.AttemptCount, message.RowVersion.ToArray());
        dbContext.ChangeTracker.Clear();
        if (claim is not null)
        {
            telemetry.RecordOutcome(WorkerTelemetry.MessagingRealtimeOutbox, "Claimed");
            if (recovered) telemetry.RecordOutcome(WorkerTelemetry.MessagingRealtimeOutbox, "LeaseRecovered");
        }
        return claim;
    }

    private async Task<bool> DispatchOneAsync(
        RealtimeClaim claim,
        CancellationToken cancellationToken)
    {
        var notifierStillRunning = false;
        try
        {
            var change = await dbContext.MessagingChangeEvents
                .AsNoTracking()
                .SingleAsync(item => item.Id == claim.ChangeEventId, cancellationToken);
            long? peerReadSequence = change.ChangeType == Domain.Messaging.MessagingChangeType.PeerReadStateChanged
                ? await dbContext.Messages.AsNoTracking().Where(m => m.Id == change.ResourceId)
                    .Select(m => (long?)m.Sequence).SingleAsync(cancellationToken)
                : null;
            var notification = new MessagingRealtimeNotification(
                change.ConversationId,
                change.ChangeType,
                change.ResourceId,
                change.ResourceVersion,
                MessagingChangeCursor.Encode(change.ConversationId, change.ChangeSequence),
                DateTime.SpecifyKind(change.OccurredAtUtc, DateTimeKind.Utc),
                change.PayloadVersion,
                peerReadSequence);
            if (!await OwnedClaim(claim).AnyAsync(cancellationToken))
            {
                telemetry.RecordFailure(WorkerTelemetry.MessagingRealtimeOutbox, "Claim");
                telemetry.RecordOutcome(WorkerTelemetry.MessagingRealtimeOutbox, "LeaseLost");
                return true;
            }

            using var notificationBudget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            notificationBudget.CancelAfter(NotificationTimeout);
            var notifying = notifier.NotifyConversationChangedAsync(notification, change.AudienceUserId, notificationBudget.Token);
            try
            {
                await notifying.WaitAsync(notificationBudget.Token);
            }
            finally
            {
                // لغو انتظار، ارسال شبکه را exactly-once نمی‌کند؛ fault کار دیررس باید مشاهده شود.
                if (!notifying.IsCompleted)
                {
                    notifierStillRunning = true;
                    _ = ObserveLateCompletionAsync(notifying);
                }
            }

            var outcome = await FinalizeClaimAsync(claim, true, cancellationToken);
            telemetry.RecordOutcome(WorkerTelemetry.MessagingRealtimeOutbox, outcome.ToString());
            if (outcome == ClaimOutcome.LeaseLost) telemetry.RecordFailure(WorkerTelemetry.MessagingRealtimeOutbox, "Claim");
            return true;
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            telemetry.RecordFailure(WorkerTelemetry.MessagingRealtimeOutbox, "Dispatch");
            if (notifierStillRunning)
            {
                // notifier خراب ممکن است همان DbContext این scope را مصرف کند؛ هیچ فرمان SQL دیگری اینجا اجرا نمی‌شود.
                logger.LogWarning("انتظار notifier پایان یافت، اما کار دیررس متوقف‌شده فرض نمی‌شود؛ batch پایان می‌یابد و lease قابل بازیابی است. Code={FailureCode}",
                    "REALTIME_NOTIFIER_COMPLETION_PENDING");
                return false;
            }
            using var completion = WorkerCompletionBudget.Create();
            var outcome = await FinalizeClaimAsync(claim, false, completion.Token);
            telemetry.RecordOutcome(WorkerTelemetry.MessagingRealtimeOutbox, outcome.ToString());
            if (outcome == ClaimOutcome.LeaseLost) return true;
            if (outcome == ClaimOutcome.Failed)
            {
                logger.LogError(
                    "اعلان realtime برای ChangeEvent {ChangeEventId} پس از سقف تلاش متوقف شد. Code={FailureCode}",
                    claim.ChangeEventId,
                    "REALTIME_OUTBOX_DISPATCH_FAILED");
            }
            else
            {
                logger.LogWarning(
                    "اعلان realtime برای ChangeEvent {ChangeEventId} ارسال نشد و دوباره تلاش می‌شود. Code={FailureCode}",
                    claim.ChangeEventId,
                    "REALTIME_OUTBOX_DISPATCH_FAILED");
            }
            return true;
        }
    }

    private async Task<ClaimOutcome> FinalizeClaimAsync(RealtimeClaim claim, bool completed, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
        var current = await dbContext.MessagingRealtimeOutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM [messaging].[RealtimeOutbox] WITH (UPDLOCK, HOLDLOCK)
            WHERE [Id] = {claim.Id}
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        // ساعت پس از انتظار واقعی قفل خوانده می‌شود؛ پارامتر زمانیِ پیش از انتظار مجوز completion نیست.
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (current is null || !current.RowVersion.SequenceEqual(claim.RowVersion) ||
            current.DispatchedAtUtc.HasValue || current.ClaimedUntilUtc is not DateTime lease || lease <= nowUtc)
        {
            await transaction.CommitAsync(cancellationToken);
            return ClaimOutcome.LeaseLost;
        }

        var owned = dbContext.MessagingRealtimeOutboxMessages.Where(item => item.Id == claim.Id &&
            item.RowVersion.SequenceEqual(claim.RowVersion) && item.DispatchedAtUtc == null);
        int updated;
        if (completed)
        {
            updated = await owned.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.DispatchedAtUtc, nowUtc)
                .SetProperty(item => item.ClaimedUntilUtc, (DateTime?)null)
                .SetProperty(item => item.NextAttemptAtUtc, (DateTime?)null), cancellationToken);
        }
        else
        {
            DateTime? nextAttempt = current.AttemptCount >= MaximumAttempts ? null :
                nowUtc.AddSeconds(Math.Min(30, Math.Max(2, current.AttemptCount * 2)));
            updated = await owned.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ClaimedUntilUtc, (DateTime?)null)
                .SetProperty(item => item.NextAttemptAtUtc, nextAttempt), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        if (updated != 1) return ClaimOutcome.LeaseLost;
        return completed ? ClaimOutcome.Completed :
            current.AttemptCount >= MaximumAttempts ? ClaimOutcome.Failed : ClaimOutcome.RetryScheduled;
    }

    private IQueryable<MessagingRealtimeOutboxMessage> OwnedClaim(RealtimeClaim claim)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        return dbContext.MessagingRealtimeOutboxMessages.Where(item => item.Id == claim.Id &&
            item.RowVersion.SequenceEqual(claim.RowVersion) && item.DispatchedAtUtc == null && item.ClaimedUntilUtc > nowUtc);
    }

    private static async Task ObserveLateCompletionAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception) { }
    }

    private enum ClaimOutcome { Completed, RetryScheduled, Failed, LeaseLost }
    private sealed record RealtimeClaim(Guid Id, Guid ChangeEventId, int AttemptCount, byte[] RowVersion);
}
