using System.Data;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed class EfCommercialAdminStore(MakanDbContext db, EfCommercialStore commerce, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : ICommercialAdminStore
{
    public async Task SetAvailabilityAsync(Guid actor, Guid session, Guid id, CommercialAvailabilityCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var plan = await db.Plans.FromSqlInterpolated($"SELECT * FROM [commerce].[Plans] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
            .SingleOrDefaultAsync(ct) ?? throw EfCommercialStore.Error(CommerceErrorCodes.ResourceNotFound);
        CheckVersion(plan.RowVersion, command.ExpectedVersion);
        var before = JsonSerializer.Serialize(new { plan.CatalogVisible, plan.NewPurchaseAvailable, plan.RenewalAvailable });
        plan.SetAvailability(command.CatalogVisible, command.NewPurchaseAvailable, command.RenewalAvailable, now);
        db.AdminAuditLogs.Add(EfCommercialStore.Audit(actor, session, "commerce.plan.availability", id, SubscriptionOwner.Individual(actor), command.Reason,
            before, JsonSerializer.Serialize(new { plan.CatalogVisible, plan.NewPurchaseAvailable, plan.RenewalAvailable, EffectiveAtUtc = now }), now, "Plan"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<CommercialValidationDto> UpdateDraftAsync(Guid actor, Guid session, Guid id, CommercialDraftCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var version = await db.PlanVersions.FromSqlInterpolated($"SELECT * FROM [commerce].[PlanVersions] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
            .Include(v => v.Price).Include(v => v.Entitlements).Include(v => v.QuotaDefinitions).SingleOrDefaultAsync(ct)
            ?? throw EfCommercialStore.Error(CommerceErrorCodes.ResourceNotFound);
        CheckVersion(version.RowVersion, command.ExpectedVersion);
        if (version.IsPublished) throw EfCommercialStore.Error(CommerceErrorCodes.UsageConflict);
        var before = Terms(version);
        var entitlementIds = version.Entitlements.Select(e => e.Id).ToHashSet();
        var quotaIds = version.QuotaDefinitions.Select(q => q.Id).ToHashSet();
        var hadPrice = version.Price is not null;
        try
        {
            if (command.ExplicitlyFree) version.SetFreePrice();
            else if (command.PriceToman.HasValue) version.SetPrice(command.PriceToman.Value * 10m, CurrencyCode.Irr);
            if (command.Calendar.HasValue) version.SetMonthlyTerms(command.Calendar.Value, command.SaleTermsReference ?? string.Empty,
                command.WarningPercent, command.CriticalWarningPercent);
            foreach (var item in command.Entitlements) version.SetEntitlement(item.Key, item.Enabled);
            foreach (var item in command.Quotas) version.SetQuota(item.Key, item.Limit);
        }
        catch (ArgumentException) { throw EfCommercialStore.Error(CommerceErrorCodes.UsageInvalid); }
        foreach (var item in version.Entitlements.Where(e => !entitlementIds.Contains(e.Id))) db.PlanEntitlements.Add(item);
        foreach (var item in version.QuotaDefinitions.Where(q => !quotaIds.Contains(q.Id))) db.PlanQuotaDefinitions.Add(item);
        if (!hadPrice && version.Price is not null) db.PlanVersionPrices.Add(version.Price);
        db.AdminAuditLogs.Add(EfCommercialStore.Audit(actor, session, "commerce.plan-version.draft.update", id, SubscriptionOwner.Individual(actor),
            command.Reason, before, Terms(version), now, "PlanVersion"));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await ValidateAsync(id, ct);
    }

    public async Task<CommercialValidationDto> ValidateAsync(Guid id, CancellationToken ct)
    {
        var version = await commerce.VersionAsync(id, ct);
        var audience = await db.Plans.Where(p => p.Id == version.PlanId).Select(p => p.Audience).SingleAsync(ct);
        var missing = version.ValidatePublication(audience);
        return new CommercialValidationDto(id, missing, missing.Count == 0 && !version.IsPublished, Convert.ToBase64String(version.RowVersion));
    }

    public async Task<CommercialSummaryDto> RegisterAsync(Guid actor, Guid session, SubscriptionOwner owner,
        RegisterCommercialContractCommand command, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await commerce.LockOwnerAsync(owner, ct);
        var version = await commerce.VersionAsync(command.PlanVersionId, ct);
        var plan = await db.Plans.SingleAsync(p => p.Id == version.PlanId, ct);
        if (plan.Audience != owner.Type || !version.IsPublished || version.ValidatePublication(owner.Type).Count != 0)
            throw EfCommercialStore.Error(CommerceErrorCodes.TermsIncomplete);
        Subscription subscription;
        try { subscription = Subscription.CreateMonthly(owner, version, command.BillingAnchorUtc, command.BillingPeriodIndex, now, null, true); }
        catch (ArgumentException) { throw EfCommercialStore.Error(CommerceErrorCodes.UsageInvalid); }
        if (subscription.EndsAtUtc <= now || subscription.StartsAtUtc > now) throw EfCommercialStore.Error(CommerceErrorCodes.UsageInvalid);
        var previous = await db.Subscriptions.Where(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id
            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled)).ToArrayAsync(ct);
        if (previous.Any(s => s.CommercialControlEnabled)) throw EfCommercialStore.Error(CommerceErrorCodes.UsageConflict);
        if (previous.Length > 0) CheckVersion(previous[0].RowVersion, command.ExpectedCurrentVersion ?? string.Empty);
        foreach (var old in previous) old.Cancel(now);
        db.Subscriptions.Add(subscription);
        db.AdminAuditLogs.Add(EfCommercialStore.Audit(actor, session, "commerce.subscription.register-managed-contract", subscription.Id,
            owner, command.Reason, string.Join(',', previous.Select(p => p.Id)),
            $"PlanVersion={version.Id:D};Start={subscription.StartsAtUtc:O};End={subscription.EndsAtUtc:O};CommercialControlEnabled=true;EffectiveAt={now:O};MigrationStatus={subscription.MigrationStatus};EnforcementFrom={subscription.CommercialEnforcementFromUtc:O}", now));
        realtime.CaptureCommerce();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await commerce.SummaryAsync(owner, now, ct);
    }

    public async Task<IReadOnlyList<CommercialHistoryDto>> HistoryAsync(Guid targetId, CancellationToken ct)
    {
        var target = targetId.ToString();
        return await db.AdminAuditLogs.AsNoTracking().Where(a => a.TargetId == target &&
            (a.Action.StartsWith("commerce.") || a.Action.StartsWith("platform.commerce."))).OrderByDescending(a => a.OccurredAtUtc)
            .Take(100).Select(a => new CommercialHistoryDto(a.Id, a.ActorUserId, a.Action, a.TargetId, a.OccurredAtUtc, a.Reason, a.BeforeSummary, a.AfterSummary)).ToArrayAsync(ct);
    }

    internal static void CheckVersion(byte[] actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected)) throw EfCommercialStore.Error(CommerceErrorCodes.UsageInvalid);
        byte[] decoded;
        try { decoded = Convert.FromBase64String(expected); } catch (FormatException) { throw EfCommercialStore.Error(CommerceErrorCodes.UsageInvalid); }
        if (decoded.Length != 8 || !actual.SequenceEqual(decoded)) throw EfCommercialStore.Error(CommerceErrorCodes.UsageConflict);
    }
    private static string Terms(PlanVersion version) => JsonSerializer.Serialize(new { version.Price?.Amount, version.Calendar,
        version.SaleTermsReference, version.WarningPercent, version.CriticalWarningPercent,
        Entitlements = version.Entitlements.Select(e => new { e.Key, e.Enabled }), Quotas = version.QuotaDefinitions.Select(q => new { q.Key, q.Limit }) });
}
