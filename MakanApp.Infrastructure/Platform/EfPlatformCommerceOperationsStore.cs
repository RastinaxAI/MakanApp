using System.Data;
using MakanApp.Application.Commerce;
using MakanApp.Application.Notification;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Reports;
using MakanApp.Infrastructure.Notification;
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
    private readonly IFileStorage _fileStorage;

    public EfPlatformCommerceOperationsStore(
        MakanDbContext dbContext,
        ICommerceStore commerceStore,
        IPaymentGateway paymentGateway,
        IPushNotificationProvider pushProvider,
        INotificationSmsProvider smsProvider,
        IFileStorage fileStorage)
    {
        _dbContext = dbContext;
        _commerceStore = commerceStore;
        _paymentGateway = paymentGateway;
        _pushProvider = pushProvider;
        _smsProvider = smsProvider;
        _fileStorage = fileStorage;
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
        var reportCounts = await _dbContext.ReportExportJobs.AsNoTracking()
            .GroupBy(item => item.Status)
            .Select(group => new PlatformJobStatusCount(
                "ReportExport",
                group.Key.ToString(),
                group.Count()))
            .ToArrayAsync(cancellationToken);
        var outboxPending = await _dbContext.OutboxMessages.AsNoTracking()
            .CountAsync(item => item.ProcessedAtUtc == null, cancellationToken);
        var messagingPending = await _dbContext.MessagingRealtimeOutboxMessages.AsNoTracking()
            .CountAsync(item => item.DispatchedAtUtc == null, cancellationToken);
        var counts = reportCounts.Concat(
        [
            new PlatformJobStatusCount("IntegrationOutbox", "Pending", outboxPending),
            new PlatformJobStatusCount("MessagingRealtimeOutbox", "Pending", messagingPending)
        ]).ToArray();
        var reportOldest = await _dbContext.ReportExportJobs.AsNoTracking()
            .Where(item => item.Status == ReportExportJobStatus.Queued)
            .MinAsync(item => (DateTime?)item.RequestedAtUtc, cancellationToken);
        var outboxOldest = await _dbContext.OutboxMessages.AsNoTracking()
            .Where(item => item.ProcessedAtUtc == null)
            .MinAsync(item => (DateTime?)item.OccurredAtUtc, cancellationToken);
        var messagingOldest = await _dbContext.MessagingRealtimeOutboxMessages.AsNoTracking()
            .Where(item => item.DispatchedAtUtc == null)
            .MinAsync(item => (DateTime?)item.OccurredAtUtc, cancellationToken);
        return new PlatformJobOperations(
            counts,
            new[] { reportOldest, outboxOldest, messagingOldest }
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .DefaultIfEmpty()
                .Min() is var oldest && oldest != default ? oldest : null);
    }

    public async Task<PlatformSystemHealth> GetSystemHealthAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var sqlAvailable = await _dbContext.Database.CanConnectAsync(cancellationToken);
        var notifications = sqlAvailable
            ? await GetNotificationOperationsAsync(nowUtc, cancellationToken)
            : new PlatformNotificationOperations(0, 0, 0, 0, null, "Unknown", "Unknown", []);
        var jobs = sqlAvailable
            ? await GetJobOperationsAsync(cancellationToken)
            : new PlatformJobOperations([], null);
        var dependencies = new[]
        {
            new PlatformDependencyHealth("Api", "Healthy", "این پاسخ از host فعال API تولید شده است."),
            new PlatformDependencyHealth(
                "SqlServer",
                sqlAvailable ? "Reachable" : "Unreachable",
                "نتیجه Database.CanConnectAsync در همین درخواست."),
            new PlatformDependencyHealth(
                "Storage",
                _fileStorage is UnavailableFileStorage ? "Unavailable" : "Configured",
                "وضعیت adapter ذخیره‌سازی؛ آزمون نوشتن destructive انجام نشد."),
            new PlatformDependencyHealth(
                "PaymentProvider",
                _paymentGateway.ProviderName.Equals("Unavailable", StringComparison.OrdinalIgnoreCase)
                    ? "Unavailable"
                    : "Configured",
                $"Provider={_paymentGateway.ProviderName}; موفقیت پرداخت از این endpoint قابل جعل نیست."),
            new PlatformDependencyHealth(
                "SignalR",
                "HostConfigured",
                "Hub در host ثبت شده است؛ اتصال client خارجی از اینجا قابل اثبات نیست.")
        };
        return new PlatformSystemHealth(
            nowUtc,
            dependencies,
            notifications,
            jobs,
            "NotIntegrated/DependencyPending");
    }

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
        UnavailablePushNotificationProvider or UnavailableNotificationSmsProvider => "Unavailable",
        DevelopmentPushNotificationProvider or DevelopmentNotificationSmsProvider => "Development",
        _ => "Configured"
    };
}
