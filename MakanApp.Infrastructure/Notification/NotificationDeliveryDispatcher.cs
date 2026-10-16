using MakanApp.Application.Notification;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Notification;
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
    ILogger<NotificationDeliveryDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                processed = await scope.ServiceProvider
                    .GetRequiredService<NotificationDeliveryProcessor>()
                    .DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SqlException exception) when (exception.Number == 208)
            {
                logger.LogDebug("جدول تلاش‌های ارسال اعلان هنوز آماده نیست.");
            }
            catch (Exception)
            {
                logger.LogError("پردازش تلاش‌های ارسال اعلان ناموفق بود.");
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var attempts = await dbContext.NotificationDeliveryAttempts
            .FromSqlInterpolated($"""
                SELECT TOP (1) *
                FROM [notification].[NotificationDeliveryAttempts] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE (
                    ([AttemptCount] < {options.MaximumAttempts} AND [Status] IN ({pending}, {deferred}) AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc}))
                    OR ([Status] = {processing} AND [ClaimedUntilUtc] <= {nowUtc})
                  )
                ORDER BY [CreatedAtUtc], [Id]
                """)
            .ToArrayAsync(cancellationToken);
        DeliveryClaim? claim = null;
        if (attempts.Length != 0)
        {
            var attempt = attempts[0];
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
            var claimedUntilUtc = timeProvider.GetUtcNow().UtcDateTime.Add(options.ClaimLease);
            var owned = await dbContext.NotificationDeliveryAttempts.Where(a => a.Id == attemptId &&
                    a.ClaimToken == claimToken && a.Status == NotificationDeliveryStatus.Processing)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.ClaimedUntilUtc, claimedUntilUtc), cancellationToken);
            if (owned == 0) return;

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(options.ProviderTimeout);
            registrationVersion = work.Push?.RegistrationVersion;
            result = work.Push is not null
                ? await pushProvider.SendAsync(work.Push, timeoutSource.Token)
                : await smsProvider.SendAsync(work.Sms!, timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            result = DeliveryProviderResult.Transient(
                NotificationDeliveryFailureReason.ProviderTimeout);
        }
        catch (Exception)
        {
            logger.LogWarning(
                "Provider ارسال اعلان برای DeliveryAttempt {DeliveryAttemptId} پاسخ قابل استفاده نداد.",
                attemptId);
            result = DeliveryProviderResult.Transient(
                NotificationDeliveryFailureReason.ProviderUnavailable);
        }

        await SaveResultAsync(attemptId, claimToken, result, CancellationToken.None, registrationVersion);
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var attempt = await dbContext.NotificationDeliveryAttempts.SingleOrDefaultAsync(
            item => item.Id == attemptId &&
                    item.ClaimToken == claimToken &&
                    item.Status == NotificationDeliveryStatus.Processing,
            cancellationToken);
        if (attempt is null)
        {
            return;
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var reason = result.FailureReason ?? NotificationDeliveryFailureReason.ProviderUnavailable;
        switch (result.Outcome)
        {
            case DeliveryProviderOutcome.Delivered:
                attempt.MarkDelivered(claimToken, nowUtc, result.ProviderMessageId);
                break;
            case DeliveryProviderOutcome.TransientFailure:
                attempt.MarkTransientFailure(
                    claimToken,
                    reason,
                    nowUtc.Add(CalculateRetryDelay(attempt.AttemptCount, result)),
                    options.MaximumAttempts);
                break;
            case DeliveryProviderOutcome.PermanentFailure:
                attempt.MarkPermanentFailure(claimToken, reason);
                break;
            case DeliveryProviderOutcome.Suppressed:
                attempt.MarkSuppressed(claimToken, reason);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }

        if (result.Outcome == DeliveryProviderOutcome.PermanentFailure &&
            reason == NotificationDeliveryFailureReason.InvalidRegistration &&
            attempt.Channel == NotificationDeliveryChannel.Push &&
            Guid.TryParseExact(attempt.DestinationReference, "N", out var registrationId) &&
            !string.IsNullOrEmpty(registrationVersion))
        {
            var version = Convert.FromBase64String(registrationVersion);
            await dbContext.PushDeviceRegistrations.Where(registration => registration.Id == registrationId &&
                    registration.RevokedAtUtc == null && registration.RowVersion.SequenceEqual(version))
                .ExecuteUpdateAsync(setters => setters.SetProperty(registration => registration.RevokedAtUtc, nowUtc), cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
