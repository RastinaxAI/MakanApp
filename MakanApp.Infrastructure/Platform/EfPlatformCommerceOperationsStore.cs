using System.Data;
using MakanApp.Application.Commerce;
using MakanApp.Application.Notification;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Platform;

public sealed partial class EfPlatformCommerceOperationsStore : IPlatformCommerceOperationsStore
{
    private readonly MakanDbContext _dbContext;
    private readonly ICommerceStore _commerceStore;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPushNotificationProvider _pushProvider;
    private readonly INotificationSmsProvider _smsProvider;
    private readonly IStorageReadinessProbe _storageReadinessProbe;
    private readonly AiServiceOptions _aiServiceOptions;
    private readonly TimeProvider _timeProvider;
    private readonly TerminalFileCleanupOptions _cleanupOptions;

    public EfPlatformCommerceOperationsStore(
        MakanDbContext dbContext,
        ICommerceStore commerceStore,
        IPaymentGateway paymentGateway,
        IPushNotificationProvider pushProvider,
        INotificationSmsProvider smsProvider,
        IStorageReadinessProbe storageReadinessProbe,
        AiServiceOptions aiServiceOptions,
        TimeProvider timeProvider,
        TerminalFileCleanupOptions cleanupOptions)
    {
        _dbContext = dbContext;
        _commerceStore = commerceStore;
        _paymentGateway = paymentGateway;
        _pushProvider = pushProvider;
        _smsProvider = smsProvider;
        _storageReadinessProbe = storageReadinessProbe;
        _aiServiceOptions = aiServiceOptions;
        _timeProvider = timeProvider;
        _cleanupOptions = cleanupOptions;
    }

    public async Task<PlatformNotificationOperations> GetNotificationOperationsAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var attempts = _dbContext.NotificationDeliveryAttempts.AsNoTracking();
        var pending = await attempts.CountAsync(
            item => item.Status == NotificationDeliveryStatus.Pending,
            cancellationToken);
        var processing = await attempts.CountAsync(
            item => item.Status == NotificationDeliveryStatus.Processing,
            cancellationToken);
        var deferred = await attempts.CountAsync(
            item => item.Status == NotificationDeliveryStatus.Deferred,
            cancellationToken);
        var failed = await attempts.CountAsync(
            item => item.Status == NotificationDeliveryStatus.Failed,
            cancellationToken);
        var oldest = await attempts
            .Where(item => item.Status == NotificationDeliveryStatus.Pending ||
                           item.Status == NotificationDeliveryStatus.Deferred)
            .MinAsync(item => (DateTime?)item.CreatedAtUtc, cancellationToken);
        var recentFailures = await attempts
            .Where(item => item.Status == NotificationDeliveryStatus.Failed ||
                           item.Status == NotificationDeliveryStatus.Deferred)
            .OrderByDescending(item => item.LastAttemptAtUtc ?? item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .Take(50)
            .Select(item => new PlatformNotificationFailureItem(
                item.Id,
                item.Channel,
                item.Status,
                item.AttemptCount,
                item.FailureReason,
                item.CreatedAtUtc,
                item.LastAttemptAtUtc,
                item.NextAttemptAtUtc))
            .ToArrayAsync(cancellationToken);
        return new PlatformNotificationOperations(
            pending,
            processing,
            deferred,
            failed,
            oldest.HasValue ? Math.Max(0, (nowUtc - oldest.Value).TotalSeconds) : null,
            ProviderAvailability(_pushProvider),
            ProviderAvailability(_smsProvider),
            recentFailures);
    }

    public async Task<PlatformJobOperations> GetJobOperationsAsync(
        CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var reportStates = await _dbContext.ReportExportJobs.AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count(), Oldest = group.Min(item => item.RequestedAtUtc) })
            .ToArrayAsync(cancellationToken);
        var outboxStates = await _dbContext.OutboxMessages.AsNoTracking()
            .Where(item => item.ProcessedAtUtc == null)
            .GroupBy(item => item.ClaimedUntilUtc > nowUtc ? "Processing" :
                item.AttemptCount >= OutboxProcessor.MaximumAttempts ? "Failed" : "Pending")
            .Select(group => new QueueState(group.Key, group.Count(), group.Min(item => item.OccurredAtUtc)))
            .ToArrayAsync(cancellationToken);
        var messagingStates = await _dbContext.MessagingRealtimeOutboxMessages.AsNoTracking()
            .Where(item => item.DispatchedAtUtc == null)
            .GroupBy(item => item.ClaimedUntilUtc > nowUtc ? "Processing" :
                item.AttemptCount >= MessagingRealtimeOutboxProcessor.MaximumAttempts ? "Failed" : "Pending")
            .Select(group => new QueueState(group.Key, group.Count(), group.Min(item => item.OccurredAtUtc)))
            .ToArrayAsync(cancellationToken);
        var scheduledCounts = await _dbContext.ScheduledMessages.AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        // قصد آینده backlog عقب‌افتاده نیست؛ سن صف از موعد رسمی کارهای سررسیدشده محاسبه می‌شود.
        var scheduledStates = await _dbContext.ScheduledMessages.AsNoTracking()
            .Where(item => item.Status == ScheduledMessageStatus.Scheduled && item.ScheduledAtUtc <= nowUtc ||
                           item.Status == ScheduledMessageStatus.Failed)
            .GroupBy(item => item.Status == ScheduledMessageStatus.Failed ? "Failed" :
                item.ClaimedUntilUtc > nowUtc ? "Processing" :
                item.AttemptCount >= ScheduledMessage.MaximumAttempts ? "Failed" : "Pending")
            .Select(group => new QueueState(group.Key, group.Count(), group.Min(item => item.ScheduledAtUtc)))
            .ToArrayAsync(cancellationToken);
        var reports = reportStates.Select(item => new QueueState(
            item.Status == ReportExportJobStatus.Queued ? "Pending" : item.Status.ToString(), item.Count, item.Oldest)).ToArray();
        var queues = new[]
        {
            QueueDetail("ReportExport", reports, nowUtc),
            QueueDetail("IntegrationOutbox", outboxStates, nowUtc),
            QueueDetail("MessagingRealtimeOutbox", messagingStates, nowUtc),
            QueueDetail("ScheduledMessage", scheduledStates, nowUtc)
        };
        var counts = reportStates.Select(item => new PlatformJobStatusCount("ReportExport", item.Status.ToString(), item.Count))
            .Concat(StatusCounts("IntegrationOutbox", outboxStates))
            .Concat(StatusCounts("MessagingRealtimeOutbox", messagingStates))
            .Concat(scheduledCounts.Select(item => new PlatformJobStatusCount("ScheduledMessage", item.Status.ToString(), item.Count)))
            .ToArray();
        return new PlatformJobOperations(counts, queues.Min(queue => queue.OldestPendingAtUtc)) { Queues = queues };
    }

    public async Task<PlatformSystemHealth> GetSystemHealthAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var sqlAvailable = await ProbeSafelyAsync(() => _dbContext.Database.CanConnectAsync(cancellationToken), cancellationToken);
        var notifications = sqlAvailable
            ? await GetNotificationOperationsAsync(nowUtc, cancellationToken)
            : new PlatformNotificationOperations(0, 0, 0, 0, null, "Unknown", "Unknown", []);
        var jobs = sqlAvailable
            ? await GetJobOperationsAsync(cancellationToken)
            : new PlatformJobOperations([], null);
        var storageAvailable = await ProbeSafelyAsync(() => _storageReadinessProbe.IsReadyAsync(cancellationToken), cancellationToken);
        var aiAvailability = _aiServiceOptions.Enabled ? "Configured/RuntimeNotProbed" : "Disabled";
        var dependencies = new[]
        {
            new PlatformDependencyHealth("Api", "Healthy", "این پاسخ از host فعال API تولید شده است."),
            new PlatformDependencyHealth(
                "SqlServer",
                sqlAvailable ? "Reachable" : "Unreachable",
                "نتیجه Database.CanConnectAsync در همین درخواست."),
            new PlatformDependencyHealth(
                "Storage",
                storageAvailable ? "Ready" : "Unavailable",
                "probe محدود storage با دادهٔ مصنوعی؛ هیچ فایل کسب‌وکاری تغییر نمی‌کند."),
            new PlatformDependencyHealth(
                "PaymentProvider",
                _paymentGateway.ProviderName.Equals("Unavailable", StringComparison.OrdinalIgnoreCase)
                    ? "Unavailable"
                    : "Configured",
                "وضعیت انتخاب adapter است؛ این بررسی موفقیت یا دسترسی runtime پرداخت را اثبات نمی‌کند."),
            new PlatformDependencyHealth(
                "SignalR",
                "HostConfigured",
                "Hub در host ثبت شده است؛ اتصال client خارجی از اینجا قابل اثبات نیست."),
            new PlatformDependencyHealth("Ai", aiAvailability,
                "وابستگی اختیاری؛ صرفاً انتخاب تنظیمات، بدون probe شبکه یا انتشار آدرس/credential."),
            new PlatformDependencyHealth("Push", ProviderAvailability(_pushProvider),
                "وابستگی اختیاری؛ پیکربندی adapter اثبات دریافت اعلان روی دستگاه نیست."),
            new PlatformDependencyHealth("FileCleanup", _cleanupOptions.Enabled ? "Enabled" : "Disabled",
                "وضعیت تنظیم مکانیزم محدود bytes terminal است، نه اثبات حذف موفق یا مجوز purge عمومی.")
        };
        return new PlatformSystemHealth(
            nowUtc,
            dependencies,
            notifications,
            jobs,
            aiAvailability);
    }

    private static PlatformJobQueueDetail QueueDetail(string name, IReadOnlyCollection<QueueState> states, DateTime nowUtc)
    {
        var oldest = states.Where(state => state.Status == "Pending").Select(state => (DateTime?)state.Oldest).Min();
        return new PlatformJobQueueDetail(name,
            states.Where(state => state.Status == "Pending").Sum(state => state.Count),
            states.Where(state => state.Status == "Processing").Sum(state => state.Count),
            states.Where(state => state.Status == "Failed").Sum(state => state.Count),
            oldest.HasValue ? DateTime.SpecifyKind(oldest.Value, DateTimeKind.Utc) : null,
            oldest.HasValue ? Math.Max(0, (nowUtc - oldest.Value).TotalSeconds) : null);
    }

    private static IEnumerable<PlatformJobStatusCount> StatusCounts(string name, IReadOnlyCollection<QueueState> states) =>
        new[] { "Pending", "Processing", "Failed" }.Select(status =>
            new PlatformJobStatusCount(name, status, states.Where(state => state.Status == status).Sum(state => state.Count)));

    private static async Task<bool> ProbeSafelyAsync(Func<Task<bool>> probe, CancellationToken cancellationToken)
    {
        try { return await probe(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return false; }
    }

    private sealed record QueueState(string Status, int Count, DateTime Oldest);

    private async Task<AdminAuditLog> CreateAuditAsync(
        PlatformActorContext actor,
        string action,
        string targetType,
        string targetId,
        Guid? organizationId,
        string reason,
        string? before,
        string? after,
        CancellationToken cancellationToken)
    {
        var personId = await _dbContext.Users.AsNoTracking()
            .Where(user => user.Id == actor.UserId)
            .Select(user => user.PersonId)
            .SingleAsync(cancellationToken);
        return AdminAuditLog.Create(
            actor.UserId,
            personId,
            actor.SessionId,
            action,
            targetType,
            targetId,
            organizationId,
            reason,
            actor.OccurredAtUtc,
            actor.CorrelationId,
            AdminAuditResult.Succeeded,
            before,
            after);
    }

    private async Task SaveCommerceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PlatformException(
                PlatformErrorCodes.PlanConflict,
                "داده تجاری هم‌زمان تغییر کرده است؛ وضعیت جدید را دریافت کنید.");
        }
        catch (DbUpdateException)
        {
            throw new PlatformException(
                PlatformErrorCodes.PlanConflict,
                "داده تجاری با رکورد موجود تعارض دارد.");
        }
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSerializableAsync(
        CancellationToken cancellationToken) =>
        _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    private static string ProviderAvailability(object provider) => provider switch
    {
        MessengerPushNotificationProvider messengerPush => messengerPush.Availability,
        UnavailablePushNotificationProvider or UnavailableNotificationSmsProvider => "Unavailable",
        DevelopmentPushNotificationProvider or DevelopmentNotificationSmsProvider => "Development",
        _ => "Configured"
    };
}
