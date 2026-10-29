using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfOrderPaymentStore
{
    private async Task SetCommercialOrderTermsAsync(Order order, PlanVersion version, CommercialOrderPurpose purpose,
        Guid? targetId, DateTime now, CancellationToken ct)
    {
        var owner = order.OrganizationId.HasValue ? SubscriptionOwner.Organization(order.OrganizationId.Value) : SubscriptionOwner.Individual(order.PurchaserUserId);
        var plan = await dbContext.Plans.FromSqlInterpolated($"SELECT * FROM [commerce].[Plans] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={version.PlanId}").SingleAsync(ct);
        if (!Enum.IsDefined(purpose) || plan.Audience != owner.Type || version.ValidatePublication(owner.Type).Count != 0
            || purpose == CommercialOrderPurpose.Purchase && !plan.NewPurchaseAvailable)
            throw EfCommercialStore.Error(CommerceErrorCodes.PlanNotPurchasable);
        var current = await commerce.CurrentAsync(owner, now, ct);
        var expires = now.AddMinutes(commercialOptions.Value.QuoteLifetimeMinutes);
        if (purpose == CommercialOrderPurpose.Purchase)
        {
            if (targetId.HasValue || current is { CommercialControlEnabled: true }) throw EfCommercialStore.Error(CommerceErrorCodes.OrderInvalid);
            order.SetCommercialTerms(purpose, null, null, now, 0, expires, version.Price!.Amount);
            return;
        }
        if (!targetId.HasValue) throw EfCommercialStore.Error(CommerceErrorCodes.OrderInvalid);
        var target = await dbContext.Subscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == targetId && s.OwnerType == owner.Type && s.OwnerId == owner.Id, ct)
            ?? throw EfCommercialStore.Error(CommerceErrorCodes.ResourceNotFound);
        if (!target.BillingAnchorUtc.HasValue || !target.EndsAtUtc.HasValue) throw EfCommercialStore.Error(CommerceErrorCodes.TermsIncomplete);
        var previousVersion = await commerce.VersionAsync(target.PlanVersionId, ct);
        if (!(previousVersion.PlanId == version.PlanId ? plan.RenewalAvailable : plan.NewPurchaseAvailable))
            throw EfCommercialStore.Error(CommerceErrorCodes.PlanNotPurchasable);
        if (previousVersion.Calendar != version.Calendar) throw EfCommercialStore.Error(CommerceErrorCodes.OrderInvalid);
        if (purpose == CommercialOrderPurpose.Renewal)
        {
            var anchor = target.BillingAnchorUtc.Value;
            var index = target.BillingPeriodIndex + 1;
            if (owner.Type == SubscriptionOwnerType.Individual && now >= target.EndsAtUtc
                || owner.Type == SubscriptionOwnerType.Organization && now >= target.EndsAtUtc.Value.AddDays(7)
                    && commercialOptions.Value.RenewalAfterGraceStartsAtPayment)
            { anchor = now; index = 0; }
            var duplicate = await dbContext.Subscriptions.AnyAsync(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id
                && s.StartsAtUtc == CommercialPeriods.MonthlyBoundary(anchor, index, version.Calendar!.Value), ct);
            if (duplicate) throw EfCommercialStore.Error(CommerceErrorCodes.UsageConflict);
            if (CommercialPeriods.MonthlyBoundary(anchor, index + 1, version.Calendar!.Value) <= now)
                throw EfCommercialStore.Error(CommerceErrorCodes.OrderInvalid);
            order.SetCommercialTerms(purpose, target.Id, target.PlanVersionId, anchor, index, expires, version.Price!.Amount);
            return;
        }
        if (current?.Id != target.Id) throw EfCommercialStore.Error(CommerceErrorCodes.SubscriptionInactive);
        var previous = await commerce.VersionAsync(target.PlanVersionId, ct);
        try { CommercialPlanChange.EnsureUpgrade(previous, version); }
        catch (InvalidOperationException) { throw EfCommercialStore.Error(CommerceErrorCodes.OrderInvalid); }
        var amendments = await dbContext.SubscriptionAmendments.AsNoTracking().Where(a => a.SubscriptionId == target.Id).ToArrayAsync(ct);
        var changes = new List<(decimal, long, long)>();
        foreach (var amendment in amendments)
        {
            var from = await commerce.VersionAsync(amendment.PreviousPlanVersionId, ct);
            var to = await commerce.VersionAsync(amendment.NextPlanVersionId, ct);
            changes.Add(((to.Price!.Amount - from.Price!.Amount) / 10m, amendment.RemainingTicks, amendment.DurationTicks));
        }
        changes.Add(((version.Price!.Amount - previous.Price!.Amount) / 10m, (target.EndsAtUtc.Value - now).Ticks,
            (target.EndsAtUtc.Value - target.StartsAtUtc).Ticks));
        var amount = CommercialProration.CumulativePriceToman(changes) - amendments.Sum(a => a.ChargedToman);
        order.SetCommercialTerms(purpose, target.Id, previous.Id, target.BillingAnchorUtc.Value, target.BillingPeriodIndex,
            expires < target.EndsAtUtc ? expires : target.EndsAtUtc.Value, amount * 10m);
    }
}
