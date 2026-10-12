using MakanApp.Application.Notification;
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

public sealed class NotificationDeliveryProcessor(
    MakanDbContext dbContext,
    IPushNotificationProvider pushProvider,
    INotificationSmsProvider smsProvider,
    NotificationDeliveryOptions options,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryProcessor> logger)
{
    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var claims = await ClaimBatchAsync(cancellationToken);
        foreach (var claim in claims)
        {
            await ProcessOneAsync(claim.Id, claim.Token, cancellationToken);
        }

        return claims.Count;
    }

    private async Task<IReadOnlyCollection<DeliveryClaim>> ClaimBatchAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var pending = (int)NotificationDeliveryStatus.Pending;
        var processing = (int)NotificationDeliveryStatus.Processing;
        var deferred = (int)NotificationDeliveryStatus.Deferred;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var attempts = await dbContext.NotificationDeliveryAttempts
            .FromSqlInterpolated($"""
                SELECT TOP ({options.BatchSize}) *
                FROM [notification].[NotificationDeliveryAttempts] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [AttemptCount] < {options.MaximumAttempts}
                  AND (
                    ([Status] IN ({pending}, {deferred}) AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc] <= {nowUtc}))
                    OR ([Status] = {processing} AND [ClaimedUntilUtc] <= {nowUtc})
                  )
                ORDER BY [CreatedAtUtc], [Id]
                """)
            .ToArrayAsync(cancellationToken);
        var claims = attempts.Select(attempt =>
        {
            var token = Guid.NewGuid();
            attempt.Claim(token, nowUtc, nowUtc.Add(options.ClaimLease));
            return new DeliveryClaim(attempt.Id, token);
        }).ToArray();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return claims;
    }

    private async Task ProcessOneAsync(
        Guid attemptId,
        Guid claimToken,
        CancellationToken cancellationToken)
    {
        DeliveryProviderResult result;
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

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(options.ProviderTimeout);
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

        await SaveResultAsync(attemptId, claimToken, result, CancellationToken.None);
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
        if (!Guid.TryParseExact(attempt.DestinationReference, "N", out var destinationId))
        {
            return new DeliveryWork(null, null, NotificationDeliveryFailureReason.ProviderRejected);
        }

        if (attempt.Channel == NotificationDeliveryChannel.Push)
        {
            var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
            var token = await (
                from registration in dbContext.PushDeviceRegistrations.AsNoTracking()
                join session in dbContext.UserSessions.AsNoTracking()
                    on new { Id = registration.UserSessionId, registration.UserId }
                    equals new { session.Id, session.UserId }
                where registration.Id == destinationId &&
                      registration.UserId == notification.RecipientUserId &&
                      registration.RevokedAtUtc == null &&
                      session.RevokedAtUtc == null &&
                      session.ExpiresAtUtc > nowUtc
                select registration.PushToken)
                .SingleOrDefaultAsync(cancellationToken);
            if (token is null)
            {
                return new DeliveryWork(null, null, NotificationDeliveryFailureReason.DeviceRevoked);
            }

            var content = NotificationExternalContentPolicy.CreatePush(notification);
            return new DeliveryWork(
                new PushNotificationDelivery(
                    attempt.Id,
                    notification.Id,
                    token,
                    content.Title,
                    content.Body,
                    content.DeepLink),
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
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
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
                    nowUtc.Add(CalculateRetryDelay(attempt.AttemptCount)),
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

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private TimeSpan CalculateRetryDelay(int attemptCount)
    {
        var multiplier = Math.Max(1, attemptCount * attemptCount);
        var ticks = Math.Min(
            options.MaximumRetryDelay.Ticks,
            options.InitialRetryDelay.Ticks * multiplier);
        return TimeSpan.FromTicks(ticks);
    }

    private sealed record DeliveryClaim(Guid Id, Guid Token);

    private sealed record DeliveryWork(
        PushNotificationDelivery? Push,
        NotificationSmsDelivery? Sms,
        NotificationDeliveryFailureReason? SuppressionReason);
}
