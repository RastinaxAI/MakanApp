using System.Diagnostics;
using System.Data;
using MakanApp.Application.Notification;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Hosting;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Notification;

public sealed class NotificationDeliveryDispatcher(
    IServiceScopeFactory scopeFactory,
    NotificationDeliveryOptions options,
    WorkerTelemetry telemetry,
    ILogger<NotificationDeliveryDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.NotificationDelivery))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    processed = await scope.ServiceProvider
                        .GetRequiredService<NotificationDeliveryProcessor>()
                        .DispatchBatchAsync(stoppingToken);
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException exception) when (exception.Number == 208)
                {
                    telemetry.RecordFailure(WorkerTelemetry.NotificationDelivery, "Loop");
                    logger.LogError("جدول تلاش‌های ارسال اعلان هنوز آماده نیست. Code={FailureCode}", "NOTIFICATION_DELIVERY_SCHEMA_UNAVAILABLE");
                }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.NotificationDelivery, "Loop");
                    logger.LogError("پردازش تلاش‌های ارسال اعلان ناموفق بود. Code={FailureCode}", "NOTIFICATION_DELIVERY_LOOP_FAILED");
                }
                finally
                {
                    telemetry.RecordBatch(WorkerTelemetry.NotificationDelivery, processed, started);
                }
            }

            if (processed < options.BatchSize)
            {
                await Task.Delay(options.IdlePollingInterval, stoppingToken);
            }
        }
    }
}

public sealed partial class NotificationDeliveryProcessor(
    MakanDbContext dbContext,
    IPushNotificationProvider pushProvider,
    INotificationSmsProvider smsProvider,
    INotificationPreferenceResolver preferenceResolver,
    IMessagingPrivacyResolver privacyResolver,
    NotificationDeliveryOptions options,
    TimeProvider timeProvider,
    WorkerTelemetry telemetry,
    ILogger<NotificationDeliveryProcessor> logger)
{
    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        if (options.ClaimLease <= options.ProviderTimeout)
            throw new InvalidOperationException("مهلت claim باید از timeout ارسال اعلان بیشتر باشد.");
        var processed = 0;
        while (processed < options.BatchSize)
        {
            var claim = await ClaimNextAsync(cancellationToken);
            if (claim is null) break;
            processed++;
            if (!claim.IsTerminal) await ProcessOneAsync(claim.Id, claim.Token, cancellationToken);
        }

        return processed;
    }

    private async Task<DeliveryClaim?> ClaimNextAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var pending = (int)NotificationDeliveryStatus.Pending;
        var processing = (int)NotificationDeliveryStatus.Processing;
        var deferred = (int)NotificationDeliveryStatus.Deferred;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var attempts = await dbContext.NotificationDeliveryAttempts
            .FromSqlInterpolated($"""
                SELECT TOP (1) *
                FROM [notification].[NotificationDeliveryAttempts] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK)
                WHERE (
                    ([AttemptCount] < {options.MaximumAttempts} AND [Status] IN ({pending}, {deferred}) AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc}))
                    OR ([Status] = {processing} AND [ClaimedUntilUtc] <= {nowUtc})
                  )
                ORDER BY [CreatedAtUtc], [Id]
                """)
            .ToArrayAsync(cancellationToken);
        DeliveryClaim? claim = null;
        var recovered = false;
        if (attempts.Length != 0)
        {
            var attempt = attempts[0];
            nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            recovered = attempt.Status == NotificationDeliveryStatus.Processing;
            if (attempt.AttemptCount >= options.MaximumAttempts)
            {
                attempt.FailExhaustedAbandonedClaim(nowUtc, options.MaximumAttempts);
                claim = new DeliveryClaim(attempt.Id, Guid.Empty, true);
            }
            else
            {
                var token = Guid.NewGuid();
                attempt.Claim(token, nowUtc, nowUtc.Add(options.ClaimLease));
                claim = new DeliveryClaim(attempt.Id, token);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        if (claim is not null)
        {
            telemetry.RecordOutcome(WorkerTelemetry.NotificationDelivery, claim.IsTerminal ? "Failed" : "Claimed");
            if (recovered && !claim.IsTerminal) telemetry.RecordOutcome(WorkerTelemetry.NotificationDelivery, "LeaseRecovered");
            if (claim.IsTerminal) logger.LogError("تلاش رهاشدهٔ اعلان به سقف رسید. Code={FailureCode}", "NOTIFICATION_DELIVERY_ATTEMPTS_EXHAUSTED");
        }
        return claim;
    }

    private async Task ProcessOneAsync(
        Guid attemptId,
        Guid claimToken,
        CancellationToken cancellationToken)
    {
        DeliveryProviderResult result;
        string? registrationVersion = null;
        try
        {
            var work = await LoadWorkAsync(attemptId, claimToken, cancellationToken);
            if (work is null)
            {
                return;
            }

            if (work.SuppressionReason.HasValue)
            {
                await SaveResultAsync(
                    attemptId,
                    claimToken,
                    DeliveryProviderResult.Suppressed(work.SuppressionReason.Value),
                    cancellationToken);
                return;
            }
            if (work.DeferUntilUtc.HasValue)
            {
                await DeferBeforeDeliveryAsync(attemptId, claimToken, work.DeferUntilUtc.Value, cancellationToken);
                return;
            }
            // بررسی مجوز ممکن است زمان ببرد؛ پیش از شبکه، مالکیت claim دوباره تثبیت می‌شود.
            if (!await TryRenewClaimAsync(attemptId, claimToken, cancellationToken)) return;

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(options.ProviderTimeout);
            registrationVersion = work.Push?.RegistrationVersion;
            var sending = work.Push is not null
                ? pushProvider.SendAsync(work.Push, timeoutSource.Token)
                : smsProvider.SendAsync(work.Sms!, timeoutSource.Token);
            try { result = await sending.WaitAsync(timeoutSource.Token); }
            catch
            {
                _ = ObserveLateProviderAsync(sending);
                throw;
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            telemetry.RecordFailure(WorkerTelemetry.NotificationDelivery, "Send");
            logger.LogError("مهلت provider اعلان تمام شد. Code={FailureCode}", "NOTIFICATION_DELIVERY_PROVIDER_TIMEOUT");
            result = DeliveryProviderResult.Transient(
                NotificationDeliveryFailureReason.ProviderTimeout);
        }
        catch (Exception)
        {
            telemetry.RecordFailure(WorkerTelemetry.NotificationDelivery, "Send");
            logger.LogError("Provider ارسال اعلان پاسخ قابل استفاده نداد. Code={FailureCode}", "NOTIFICATION_DELIVERY_PROVIDER_UNAVAILABLE");
            result = DeliveryProviderResult.Transient(
                NotificationDeliveryFailureReason.ProviderUnavailable);
        }

        using var completion = WorkerCompletionBudget.Create();
        await SaveResultAsync(attemptId, claimToken, result, completion.Token, registrationVersion);
    }

    private async Task<DeliveryWork?> LoadWorkAsync(
        Guid attemptId,
        Guid claimToken,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var attempt = await dbContext.NotificationDeliveryAttempts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == attemptId &&
                        item.ClaimToken == claimToken &&
                        item.Status == NotificationDeliveryStatus.Processing,
                cancellationToken);
        if (attempt is null)
        {
            return null;
        }

        var notification = await dbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item => item.Id == attempt.NotificationId, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var currentAccess = await EvaluateCurrentDeliveryAsync(attempt, notification, nowUtc, cancellationToken);
        if (currentAccess is not null) return currentAccess;
        if (!Guid.TryParseExact(attempt.DestinationReference, "N", out var destinationId))
        {
            return new DeliveryWork(null, null, NotificationDeliveryFailureReason.ProviderRejected);
        }

        if (attempt.Channel == NotificationDeliveryChannel.Push)
        {
            var device = await (
                from registration in dbContext.PushDeviceRegistrations.AsNoTracking()
                join session in dbContext.UserSessions.AsNoTracking()
                    on new { Id = registration.UserSessionId, registration.UserId }
                    equals new { session.Id, session.UserId }
                where registration.Id == destinationId &&
                      registration.UserId == notification.RecipientUserId &&
                      registration.RevokedAtUtc == null &&
                      session.RevokedAtUtc == null &&
                      session.ExpiresAtUtc > nowUtc && session.AbsoluteExpiresAtUtc > nowUtc
                select registration)
                .SingleOrDefaultAsync(cancellationToken);
            if (device is null)
            {
                return new DeliveryWork(null, null, NotificationDeliveryFailureReason.DeviceRevoked);
            }

            var content = NotificationExternalContentPolicy.CreatePush(notification);
            return new DeliveryWork(
                new PushNotificationDelivery(
                    attempt.Id,
                    notification.Id,
                    device.PushToken,
                    content.Title,
                    content.Body,
                    content.DeepLink,
                    device.Platform,
                    Convert.ToBase64String(device.RowVersion),
                    device.WebPushP256dh,
                    device.WebPushAuth,
                    notification.Type,
                    notification.ResourceType == NotificationResourceType.Conversation ? notification.ResourceId : null),
                null,
                null);
        }

        var destination = await dbContext.UserCredentials
            .AsNoTracking()
            .Where(credential => credential.Id == destinationId &&
                                 credential.UserId == notification.RecipientUserId &&
                                 credential.Kind == CredentialKind.MobilePhone)
            .Select(credential => credential.NormalizedIdentifier)
            .SingleOrDefaultAsync(cancellationToken);
        if (destination is null)
        {
            return new DeliveryWork(null, null, NotificationDeliveryFailureReason.NoSmsDestination);
        }

        var sms = NotificationExternalContentPolicy.CreateSms(notification);
        return new DeliveryWork(
            null,
            new NotificationSmsDelivery(
                attempt.Id,
                notification.Id,
                destination,
                sms.TemplateKey,
                sms.Parameters),
            null);
    }

    private async Task SaveResultAsync(
        Guid attemptId,
        Guid claimToken,
        DeliveryProviderResult result,
        CancellationToken cancellationToken,
        string? registrationVersion = null)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var attempt = await LockAttemptAsync(attemptId, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!IsLiveOwner(attempt, claimToken, nowUtc))
        {
            await transaction.CommitAsync(cancellationToken);
            telemetry.RecordOutcome(WorkerTelemetry.NotificationDelivery, "LeaseLost");
            return;
        }

        var ownedAttempt = attempt!;
        var reason = result.FailureReason ?? NotificationDeliveryFailureReason.ProviderUnavailable;
        switch (result.Outcome)
        {
            case DeliveryProviderOutcome.Delivered:
                ownedAttempt.MarkDelivered(claimToken, nowUtc, result.ProviderMessageId);
                break;
            case DeliveryProviderOutcome.TransientFailure:
                ownedAttempt.MarkTransientFailure(
                    claimToken,
                    reason,
                    nowUtc.Add(CalculateRetryDelay(ownedAttempt.AttemptCount, result)),
                    options.MaximumAttempts);
                break;
            case DeliveryProviderOutcome.PermanentFailure:
                ownedAttempt.MarkPermanentFailure(claimToken, reason);
                break;
            case DeliveryProviderOutcome.Suppressed:
                ownedAttempt.MarkSuppressed(claimToken, reason);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }

        if (result.Outcome == DeliveryProviderOutcome.PermanentFailure &&
            reason == NotificationDeliveryFailureReason.InvalidRegistration &&
            ownedAttempt.Channel == NotificationDeliveryChannel.Push &&
            Guid.TryParseExact(ownedAttempt.DestinationReference, "N", out var registrationId) &&
            !string.IsNullOrEmpty(registrationVersion))
        {
            var version = Convert.FromBase64String(registrationVersion);
            await dbContext.PushDeviceRegistrations.Where(registration => registration.Id == registrationId &&
                    registration.RevokedAtUtc == null && registration.RowVersion.SequenceEqual(version))
                .ExecuteUpdateAsync(setters => setters.SetProperty(registration => registration.RevokedAtUtc, nowUtc), cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        telemetry.RecordOutcome(WorkerTelemetry.NotificationDelivery, ownedAttempt.Status switch
        {
            NotificationDeliveryStatus.Delivered => "Completed",
            NotificationDeliveryStatus.Deferred => "RetryScheduled",
            NotificationDeliveryStatus.Suppressed => "Suppressed",
            _ => "Failed"
        });
        if (result.Outcome is DeliveryProviderOutcome.TransientFailure or DeliveryProviderOutcome.PermanentFailure)
        {
            telemetry.RecordFailure(WorkerTelemetry.NotificationDelivery, "Process");
            logger.LogError("نتیجهٔ ناموفق provider اعلان به‌صورت پایدار ثبت شد. Code={FailureCode}", "NOTIFICATION_DELIVERY_PROVIDER_FAILED");
        }
    }

    private async Task<bool> TryRenewClaimAsync(Guid attemptId, Guid claimToken, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var attempt = await LockAttemptAsync(attemptId, cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!IsLiveOwner(attempt, claimToken, nowUtc))
        {
            await transaction.CommitAsync(cancellationToken);
            telemetry.RecordOutcome(WorkerTelemetry.NotificationDelivery, "LeaseLost");
            return false;
        }
        dbContext.Entry(attempt!).Property(item => item.ClaimedUntilUtc).CurrentValue = nowUtc.Add(options.ClaimLease);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private Task<NotificationDeliveryAttempt?> LockAttemptAsync(Guid attemptId, CancellationToken cancellationToken) =>
        dbContext.NotificationDeliveryAttempts.FromSqlInterpolated($"""
            SELECT * FROM [notification].[NotificationDeliveryAttempts] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE [Id] = {attemptId}
            """).SingleOrDefaultAsync(cancellationToken);

    private static bool IsLiveOwner(NotificationDeliveryAttempt? attempt, Guid token, DateTime nowUtc) =>
        attempt is { Status: NotificationDeliveryStatus.Processing } && attempt.ClaimToken == token && attempt.ClaimedUntilUtc > nowUtc;

    private static async Task ObserveLateProviderAsync(Task<DeliveryProviderResult> sending)
    {
        try { await sending; }
        catch (Exception) { /* خطای دیررس فقط مشاهده می‌شود؛ هیچ state یا متن provider منتشر نمی‌شود. */ }
    }

    private TimeSpan CalculateRetryDelay(int attemptCount, DeliveryProviderResult result)
    {
        var multiplier = 1L << Math.Clamp(attemptCount - 1, 0, 20);
        var ticks = options.InitialRetryDelay.Ticks > options.MaximumRetryDelay.Ticks / multiplier
            ? options.MaximumRetryDelay.Ticks
            : options.InitialRetryDelay.Ticks * multiplier;
        var delay = TimeSpan.FromTicks(ticks);
        if (result.FailureReason == NotificationDeliveryFailureReason.ProviderRateLimited && delay < TimeSpan.FromMinutes(1))
            delay = TimeSpan.FromMinutes(1);
        if (result.RetryAfter > delay) delay = result.RetryAfter.Value;
        return delay;
    }

    private sealed record DeliveryClaim(Guid Id, Guid Token, bool IsTerminal = false);

    private sealed record DeliveryWork(
        PushNotificationDelivery? Push,
        NotificationSmsDelivery? Sms,
        NotificationDeliveryFailureReason? SuppressionReason,
        DateTime? DeferUntilUtc = null);
}
