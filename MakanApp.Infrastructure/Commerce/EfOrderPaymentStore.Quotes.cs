using System.Data;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfOrderPaymentStore
{
    public async Task<CommercialQuoteDto> QuoteAsync(Guid userId, Guid? organizationId, CreateOrderCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var owner = organizationId.HasValue ? SubscriptionOwner.Organization(organizationId.Value) : SubscriptionOwner.Individual(userId);
        await commerce.LockOwnerAsync(owner, ct);
        var version = await commerce.VersionAsync(command.PlanVersionId, ct);
        if (!version.IsPublished || version.Price is null) throw EfCommercialStore.Error(CommerceErrorCodes.PlanNotPurchasable);
        var order = Order.Create(userId, organizationId, version, Guid.NewGuid(), new byte[32], now);
        await SetCommercialOrderTermsAsync(order, version, command.Purpose, command.SubscriptionId, now, ct);
        var quotas = new List<CommercialQuotaDto>();
        var current = command.SubscriptionId.HasValue ? await dbContext.Subscriptions.AsNoTracking().SingleAsync(s => s.Id == command.SubscriptionId, ct) : null;
        foreach (var quota in version.QuotaDefinitions)
        {
            var consumed = QuotaKinds.For(quota.Key) == QuotaKind.CurrentCapacity ? await commerce.CapacityAsync(owner, quota.Key, ct) : 0;
            long reserved = 0;
            var limit = quota.Limit;
            if (command.Purpose == CommercialOrderPurpose.Upgrade && current is not null)
            {
                var previousVersion = await commerce.VersionAsync(current.PlanVersionId, ct);
                if (previousVersion.QuotaDefinitions.Any(q => q.Key == quota.Key))
                {
                    var totals = await commerce.QuotaAsync(current, previousVersion, quota.Key, ct);
                    consumed = totals.Consumed; reserved = totals.Reserved;
                }
                if (QuotaKinds.For(quota.Key) == QuotaKind.PeriodicConsumption && limit.HasValue)
                    limit = await ProjectedLimitAsync(current, version, quota.Key, now, ct);
            }
            var available = quota.Key is not (QuotaKey.SourceCount or QuotaKey.AssignmentFileBytes)
                && (quota.Key != QuotaKey.StorageBytes || owner.Type == SubscriptionOwnerType.Organization);
            quotas.Add(new CommercialQuotaDto(quota.Key, QuotaKinds.For(quota.Key), QuotaKinds.Unit(quota.Key), limit, consumed, reserved,
                available && limit.HasValue ? Math.Max(0, limit.Value - consumed - reserved) : null,
                limit.HasValue && (consumed > limit || reserved > limit.Value - consumed), version.WarningPercent)
                { CriticalWarningPercent = version.CriticalWarningPercent, IsUsageAvailable = available });
        }
        var effective = command.Purpose == CommercialOrderPurpose.Upgrade ? now
            : CommercialPeriods.MonthlyBoundary(order.BillingAnchorUtc!.Value, order.BillingPeriodIndex, version.Calendar!.Value);
        var end = command.Purpose == CommercialOrderPurpose.Upgrade ? current!.EndsAtUtc!.Value
            : CommercialPeriods.MonthlyBoundary(order.BillingAnchorUtc!.Value, order.BillingPeriodIndex + 1, version.Calendar!.Value);
        await tx.CommitAsync(ct);
        return new CommercialQuoteDto(command.Purpose, version.Id, command.SubscriptionId, order.Amount / 10m, "TOMAN", effective, end,
            quotas, false) { ExpiresAtUtc = order.TermsExpireAtUtc!.Value,
                StartsUponVerifiedPayment = command.Purpose == CommercialOrderPurpose.Purchase
                    || command.Purpose == CommercialOrderPurpose.Renewal && order.BillingPeriodIndex == 0 && order.BillingAnchorUtc == order.CreatedAtUtc };
    }

    private async Task<long> ProjectedLimitAsync(Subscription current, PlanVersion next, QuotaKey key, DateTime at, CancellationToken ct)
    {
        var initial = await commerce.VersionAsync(current.InitialPlanVersionId, ct);
        var amendments = await dbContext.SubscriptionAmendments.AsNoTracking().Where(a => a.SubscriptionId == current.Id).ToArrayAsync(ct);
        var deltas = new List<ProratedQuota>();
        foreach (var amendment in amendments)
        {
            var before = await commerce.VersionAsync(amendment.PreviousPlanVersionId, ct);
            var after = await commerce.VersionAsync(amendment.NextPlanVersionId, ct);
            deltas.Add(new ProratedQuota((after.QuotaDefinitions.SingleOrDefault(q => q.Key == key)?.Limit ?? 0)
                - (before.QuotaDefinitions.SingleOrDefault(q => q.Key == key)?.Limit ?? 0), amendment.RemainingTicks, amendment.DurationTicks));
        }
        var previous = await commerce.VersionAsync(current.PlanVersionId, ct);
        deltas.Add(new ProratedQuota(next.QuotaDefinitions.Single(q => q.Key == key).Limit!.Value
            - (previous.QuotaDefinitions.SingleOrDefault(q => q.Key == key)?.Limit ?? 0),
            (current.EndsAtUtc!.Value - at).Ticks, (current.EndsAtUtc.Value - current.StartsAtUtc).Ticks));
        return CommercialProration.EffectiveLimit(initial.QuotaDefinitions.SingleOrDefault(q => q.Key == key)?.Limit ?? 0, deltas);
    }
}
