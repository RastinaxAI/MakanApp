using System.Data;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed class EfCommerceStore(MakanDbContext dbContext) : ICommerceStore
{
    public async Task<CommerceSubscriptionSnapshot?> GetLatestSubscriptionAsync(
        Guid organizationId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var row = await (
            from subscription in dbContext.Subscriptions
            join planVersion in dbContext.PlanVersions
                on subscription.PlanVersionId equals planVersion.Id
            join plan in dbContext.Plans
                on planVersion.PlanId equals plan.Id
            where subscription.OrganizationId == organizationId
            orderby subscription.Status == SubscriptionStatus.Active descending,
                subscription.CreatedAtUtc descending
            select new SubscriptionRow(
                subscription.Id,
                subscription.OrganizationId,
                plan.Id,
                planVersion.Id,
                plan.Code,
                plan.DisplayName,
                planVersion.VersionNumber,
                planVersion.Status,
                subscription.Status,
                subscription.StartsAtUtc,
                subscription.EndsAtUtc))
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var entitlements = await dbContext.PlanEntitlements
            .AsNoTracking()
            .Where(item => item.PlanVersionId == row.PlanVersionId)
            .ToDictionaryAsync(item => item.Key, item => item.Enabled, cancellationToken);
        var quotas = await dbContext.PlanQuotaDefinitions
            .AsNoTracking()
            .Where(item => item.PlanVersionId == row.PlanVersionId)
            .ToDictionaryAsync(item => item.Key, item => item.Limit, cancellationToken);
        var isEffective = row.PlanVersionStatus == PlanVersionStatus.Published &&
                          row.Status == SubscriptionStatus.Active &&
                          row.StartsAtUtc <= utcNow &&
                          (!row.EndsAtUtc.HasValue || utcNow < row.EndsAtUtc.Value);

        return new CommerceSubscriptionSnapshot(
            row.SubscriptionId,
            row.OrganizationId,
            row.PlanId,
            row.PlanVersionId,
            row.PlanCode,
            row.PlanDisplayName,
            row.PlanVersionNumber,
            row.Status,
            row.StartsAtUtc,
            row.EndsAtUtc,
            isEffective,
            entitlements,
            quotas);
    }

    public async Task<IReadOnlyDictionary<QuotaKey, long>> GetCurrentUsageAsync(
        Guid organizationId,
        Guid subscriptionId,
        IReadOnlyCollection<QuotaKey> quotaKeys,
        CancellationToken cancellationToken)
    {
        var requested = quotaKeys.Distinct().ToHashSet();
        var result = new Dictionary<QuotaKey, long>(requested.Count);

        if (requested.Contains(QuotaKey.ActiveLearners))
        {
            result[QuotaKey.ActiveLearners] = await dbContext.Enrollments
                .AsNoTracking()
                .Where(enrollment =>
                    enrollment.OrganizationId == organizationId &&
                    enrollment.Status == EnrollmentStatus.Active &&
                    enrollment.EndedAtUtc == null)
                .Select(enrollment => enrollment.LearnerOrganizationPersonId)
                .Distinct()
                .LongCountAsync(cancellationToken);
        }

        if (requested.Contains(QuotaKey.ActiveClasses))
        {
            result[QuotaKey.ActiveClasses] = await dbContext.Classes
                .AsNoTracking()
                .LongCountAsync(academicClass =>
                    academicClass.OrganizationId == organizationId &&
                    academicClass.Status == ClassStatus.Active &&
                    academicClass.EndedAtUtc == null,
                    cancellationToken);
        }

        if (requested.Contains(QuotaKey.StorageBytes))
        {
            result[QuotaKey.StorageBytes] = await dbContext.FileAssets
                .AsNoTracking()
                .Where(file =>
                    file.OrganizationId == organizationId &&
                    file.Status == FileAssetStatus.Ready)
                .SumAsync(file => (long?)file.SizeBytes, cancellationToken) ?? 0;
        }

        if (requested.Contains(QuotaKey.ReportExports))
        {
            result[QuotaKey.ReportExports] = await dbContext.UsageLedgerEntries
                .AsNoTracking()
                .Where(entry =>
                    entry.OrganizationId == organizationId &&
                    entry.SubscriptionId == subscriptionId &&
                    entry.QuotaKey == QuotaKey.ReportExports)
                .SumAsync(entry => (long?)entry.Amount, cancellationToken) ?? 0;
        }

        return result;
    }

    public async Task<UsageRecordStoreResult> RecordMeteredUsageAsync(
        Guid organizationId,
        QuotaKey key,
        long amount,
        Guid sourceOperationId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var subscription = await dbContext.Subscriptions
            .FromSqlInterpolated(
                $"SELECT * FROM [commerce].[Subscriptions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Status] = {(int)SubscriptionStatus.Active}")
            .SingleOrDefaultAsync(cancellationToken);
        if (subscription is null)
        {
            var hasHistory = await dbContext.Subscriptions
                .AsNoTracking()
                .AnyAsync(item => item.OrganizationId == organizationId, cancellationToken);
            return new UsageRecordStoreResult(
                hasHistory
                    ? UsageRecordStoreStatus.SubscriptionInactive
                    : UsageRecordStoreStatus.SubscriptionRequired,
                null,
                null,
                0,
                null);
        }

        if (!subscription.IsEffectiveAt(occurredAtUtc))
        {
            return new UsageRecordStoreResult(
                UsageRecordStoreStatus.SubscriptionInactive,
                null,
                subscription.Id,
                0,
                null);
        }

        var versionIsPublished = await dbContext.PlanVersions
            .AsNoTracking()
            .AnyAsync(
                version => version.Id == subscription.PlanVersionId &&
                           version.Status == PlanVersionStatus.Published,
                cancellationToken);
        if (!versionIsPublished)
        {
            return new UsageRecordStoreResult(
                UsageRecordStoreStatus.SubscriptionInactive,
                null,
                subscription.Id,
                0,
                null);
        }

        var quota = await dbContext.PlanQuotaDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                definition => definition.PlanVersionId == subscription.PlanVersionId &&
                              definition.Key == key,
                cancellationToken);
        if (quota is null)
        {
            return new UsageRecordStoreResult(
                UsageRecordStoreStatus.QuotaRequired,
                null,
                subscription.Id,
                0,
                null);
        }

        var existing = await dbContext.UsageLedgerEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.SubscriptionId == subscription.Id &&
                         entry.QuotaKey == key &&
                         entry.SourceOperationId == sourceOperationId,
                cancellationToken);
        var currentUsage = await dbContext.UsageLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.SubscriptionId == subscription.Id && entry.QuotaKey == key)
            .SumAsync(entry => (long?)entry.Amount, cancellationToken) ?? 0;
        if (existing is not null)
        {
            return new UsageRecordStoreResult(
                existing.Amount == amount
                    ? UsageRecordStoreStatus.AlreadyRecorded
                    : UsageRecordStoreStatus.Conflict,
                existing.Id,
                subscription.Id,
                currentUsage,
                quota.Limit);
        }

        if (quota.Limit.HasValue &&
            (currentUsage > quota.Limit.Value || amount > quota.Limit.Value - currentUsage))
        {
            return new UsageRecordStoreResult(
                UsageRecordStoreStatus.QuotaExceeded,
                null,
                subscription.Id,
                currentUsage,
                quota.Limit);
        }

        var entry = UsageLedgerEntry.Create(
            organizationId,
            subscription.Id,
            key,
            amount,
            sourceOperationId,
            occurredAtUtc);
        dbContext.UsageLedgerEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new UsageRecordStoreResult(
            UsageRecordStoreStatus.Recorded,
            entry.Id,
            subscription.Id,
            checked(currentUsage + amount),
            quota.Limit);
    }

    private sealed record SubscriptionRow(
        Guid SubscriptionId,
        Guid OrganizationId,
        Guid PlanId,
        Guid PlanVersionId,
        string PlanCode,
        string PlanDisplayName,
        int PlanVersionNumber,
        PlanVersionStatus PlanVersionStatus,
        SubscriptionStatus Status,
        DateTime StartsAtUtc,
        DateTime? EndsAtUtc);
}
