using System.Diagnostics;
using MakanApp.Application.Messaging;
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
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException exception) when (exception.Number == 208)
                {
                    telemetry.RecordFailure(WorkerTelemetry.MessagingRealtimeOutbox, "Loop");
                    logger.LogDebug("جدول Outbox پیام‌رسانی هنوز آماده نیست.");
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

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var pending = await dbContext.MessagingRealtimeOutboxMessages
            .FromSqlInterpolated($"""
                SELECT TOP ({BatchSize}) *
                FROM [messaging].[RealtimeOutbox] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [DispatchedAtUtc] IS NULL
                  AND [AttemptCount] < {MaximumAttempts}
                  AND ([ClaimedUntilUtc] IS NULL OR [ClaimedUntilUtc] <= {nowUtc})
                  AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc})
                ORDER BY [OccurredAtUtc], [Id]
                """)
            .ToArrayAsync(cancellationToken);
        foreach (var message in pending)
        {
            message.Claim(nowUtc, nowUtc.Add(ClaimLease));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var message in pending)
        {
            await DispatchOneAsync(message, cancellationToken);
        }

        return pending.Length;
    }

    private async Task DispatchOneAsync(
        MessagingRealtimeOutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var change = await dbContext.MessagingChangeEvents
                .AsNoTracking()
                .SingleAsync(item => item.Id == outboxMessage.ChangeEventId, cancellationToken);
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
            await notifier.NotifyConversationChangedAsync(
                notification,
                change.AudienceUserId,
                cancellationToken);
            outboxMessage.MarkDispatched(timeProvider.GetUtcNow().UtcDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            telemetry.RecordFailure(WorkerTelemetry.MessagingRealtimeOutbox, "Dispatch");
            var retrySeconds = Math.Min(30, Math.Max(2, outboxMessage.AttemptCount * 2));
            outboxMessage.ScheduleRetry(timeProvider.GetUtcNow().UtcDateTime.AddSeconds(retrySeconds));
            await dbContext.SaveChangesAsync(CancellationToken.None);
            if (outboxMessage.AttemptCount >= MaximumAttempts)
            {
                logger.LogError(
                    "اعلان realtime برای ChangeEvent {ChangeEventId} پس از سقف تلاش متوقف شد. Code={FailureCode}",
                    outboxMessage.ChangeEventId,
                    "REALTIME_OUTBOX_DISPATCH_FAILED");
            }
            else
            {
                logger.LogWarning(
                    "اعلان realtime برای ChangeEvent {ChangeEventId} ارسال نشد و دوباره تلاش می‌شود. Code={FailureCode}",
                    outboxMessage.ChangeEventId,
                    "REALTIME_OUTBOX_DISPATCH_FAILED");
            }
        }
    }
}
