using System.Data;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfCommercialStore(MakanDbContext db, MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime) : ICommercialStore
{
    public async Task<IReadOnlyList<CommercialPlanDto>> CatalogAsync(SubscriptionOwnerType audience, DateTime utcNow, CancellationToken ct)
    {
        var plans = await db.Plans.AsNoTracking().Where(p => p.Audience == audience && p.CatalogVisible && p.NewPurchaseAvailable
            && (!p.AvailabilityEffectiveAtUtc.HasValue || p.AvailabilityEffectiveAtUtc <= utcNow)).OrderBy(p => p.Code).Take(100).ToArrayAsync(ct);
        var result = new List<CommercialPlanDto>();
        foreach (var plan in plans)
        {
            var version = await db.PlanVersions.AsNoTracking().Include(v => v.Price).Include(v => v.Entitlements).Include(v => v.QuotaDefinitions)
                .Where(v => v.PlanId == plan.Id && v.Status == PlanVersionStatus.Published && v.PublishedAtUtc <= utcNow)
                .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
            if (version is not null && version.ValidatePublication(plan.Audience).Count == 0)
                result.Add(new CommercialPlanDto(plan.Id, version.Id, plan.Code, plan.DisplayName, plan.Audience, version.VersionNumber,
                    version.Price!.Amount / 10m, "TOMAN", version.Calendar!.Value, "Manual", false,
                    version.Entitlements.ToDictionary(e => e.Key, e => e.Enabled), version.QuotaDefinitions.ToDictionary(q => q.Key, q => q.Limit)));
        }
        return result;
    }

    public async Task<CommercialSummaryDto> SummaryAsync(SubscriptionOwner owner, DateTime utcNow, CancellationToken ct)
    {
        var pending = await db.Orders.AsNoTracking().AnyAsync(o => o.Status == OrderStatus.PendingPayment &&
            (owner.Type == SubscriptionOwnerType.Individual ? o.OrganizationId == null && o.PurchaserUserId == owner.Id : o.OrganizationId == owner.Id), ct);
        var subscription = await CurrentAsync(owner, utcNow, ct);
        subscription ??= await db.Subscriptions.AsNoTracking().Where(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id && s.StartsAtUtc <= utcNow)
            .OrderByDescending(s => s.CommercialControlEnabled).ThenByDescending(s => s.StartsAtUtc).ThenByDescending(s => s.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (subscription is null || owner.Type == SubscriptionOwnerType.Organization && !subscription.CommercialControlEnabled)
            return new CommercialSummaryDto(owner.Type, owner.Id, subscription is not null ? SubscriptionDisplayState.LegacyPendingContract
                    : pending ? SubscriptionDisplayState.PendingPayment : SubscriptionDisplayState.NoSubscription,
                false, null, null, null, null, null, null, new Dictionary<EntitlementKey, bool>(), [], utcNow)
                { HasPendingPayment = pending, LegacySubscriptionId = subscription?.Id,
                    Version = subscription is null ? null : Convert.ToBase64String(subscription.RowVersion),
                    MigrationStatus = owner.Type == SubscriptionOwnerType.Organization
                        ? CommercialMigrationStatus.AwaitingValidContract : CommercialMigrationStatus.NotApplicable };
        var effective = subscription.IsEffectiveAt(utcNow);
        var grace = subscription.IsInOrganizationGrace(utcNow);
        var version = await VersionAsync(subscription.PlanVersionId, ct);
        var plan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == version.PlanId, ct);
        PlanVersion? renewal = null;
        if (subscription.ScheduledPlanVersionId.HasValue)
        {
            var proposed = await VersionAsync(subscription.ScheduledPlanVersionId.Value, ct);
            var proposedPlan = await db.Plans.AsNoTracking().SingleAsync(p => p.Id == proposed.PlanId, ct);
            if (proposedPlan.Audience == owner.Type && (proposed.PlanId == plan.Id ? proposedPlan.RenewalAvailable : proposedPlan.NewPurchaseAvailable)) renewal = proposed;
        }
        else if (plan.RenewalAvailable)
            renewal = await db.PlanVersions.AsNoTracking().Include(v => v.Price).Include(v => v.Entitlements).Include(v => v.QuotaDefinitions)
                .Where(v => v.PlanId == plan.Id && v.Status == PlanVersionStatus.Published && v.Calendar == version.Calendar)
                .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
        if (renewal is not null && renewal.ValidatePublication().Count != 0) renewal = null;
        var quotas = new List<CommercialQuotaDto>();
        if (effective || grace)
            foreach (var quota in version.QuotaDefinitions)
                quotas.Add(await QuotaAsync(subscription, version, quota.Key, ct));
        var state = !effective ? grace ? SubscriptionDisplayState.GracePeriod : SubscriptionDisplayState.Expired
            : subscription.ScheduledPlanVersionId.HasValue ? SubscriptionDisplayState.ScheduledDowngrade
            : quotas.Any(q => q.IsOverQuota || q.Limit.HasValue && q.Remaining == 0) ? SubscriptionDisplayState.QuotaExceeded
            : quotas.Any(q => q.WarningLevel != "None")
                ? SubscriptionDisplayState.QuotaWarning : SubscriptionDisplayState.Active;
        return new CommercialSummaryDto(owner.Type, owner.Id, state, subscription.CommercialControlEnabled, subscription.Id,
            version.Id, subscription.StartsAtUtc, subscription.EndsAtUtc,
            owner.Type == SubscriptionOwnerType.Organization && subscription.CommercialControlEnabled ? subscription.EndsAtUtc?.AddDays(7) : null,
            subscription.ScheduledPlanVersionId, effective || grace ? version.Entitlements.ToDictionary(e => e.Key, e => e.Enabled)
                : new Dictionary<EntitlementKey, bool>(), quotas, utcNow) { Version = Convert.ToBase64String(subscription.RowVersion),
                    MigrationStatus = subscription.MigrationStatus, CommercialEnforcementFromUtc = subscription.CommercialEnforcementFromUtc,
                    PlanCode = plan.Code, PlanDisplayName = plan.DisplayName, PlanVersionNumber = version.VersionNumber,
                    MonthlyPriceToman = version.Price?.Amount / 10m, HasPendingPayment = pending,
                    RenewalPlanVersionId = renewal?.Id, RenewalPriceToman = renewal?.Price?.Amount / 10m };
    }

    public Task<bool> CanAccessPersonalClassAsync(Guid userId, Guid classId, CancellationToken ct) => db.PersonalClasses.AsNoTracking()
        .AnyAsync(c => c.Id == classId && (c.HostUserId == userId || db.PersonalClassParticipants.Any(p => p.PersonalClassId == c.Id
            && p.StudentUserId == userId && p.EndedAtUtc == null)), ct);

    public async Task<Subscription?> CurrentAsync(SubscriptionOwner owner, DateTime utcNow, CancellationToken ct) =>
        await db.Subscriptions.AsNoTracking().Where(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id
            && (s.OwnerType == SubscriptionOwnerType.Individual || s.CommercialControlEnabled)
            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled)
            && s.StartsAtUtc <= utcNow && (!s.EndsAtUtc.HasValue || s.EndsAtUtc > utcNow))
            .OrderByDescending(s => s.StartsAtUtc).ThenByDescending(s => s.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public async Task<PlanVersion> VersionAsync(Guid id, CancellationToken ct) => await db.PlanVersions.AsNoTracking()
        .Include(v => v.Price).Include(v => v.Entitlements).Include(v => v.QuotaDefinitions).SingleOrDefaultAsync(v => v.Id == id, ct)
        ?? throw Error(CommerceErrorCodes.ResourceNotFound);

    public async Task LockOwnerAsync(SubscriptionOwner owner, CancellationToken ct, bool requireActive = true)
    {
        if (owner.Type == SubscriptionOwnerType.Individual)
        {
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={owner.Id}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (user is null || requireActive && user.AccountStatus != UserAccountStatus.Active) throw Denied();
        }
        else if (!await db.Organizations.FromSqlInterpolated($"SELECT * FROM [organization].[Organizations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={owner.Id}")
            .AsNoTracking().AnyAsync(ct)) throw Denied();
    }

    public async Task<CommercialQuotaDto> QuotaAsync(Subscription subscription, PlanVersion version, QuotaKey key, CancellationToken ct)
    {
        var definition = version.QuotaDefinitions.SingleOrDefault(q => q.Key == key);
        if (definition is null) throw Error(CommerceErrorCodes.QuotaRequired);
        long? limit = definition.Limit;
        if (limit.HasValue && QuotaKinds.For(key) == QuotaKind.PeriodicConsumption && subscription.InitialPlanVersionId != version.Id)
        {
            var initial = await VersionAsync(subscription.InitialPlanVersionId, ct);
            var initialQuota = initial.QuotaDefinitions.SingleOrDefault(q => q.Key == key);
            var changes = await db.SubscriptionAmendments.AsNoTracking().Where(a => a.SubscriptionId == subscription.Id).OrderBy(a => a.EffectiveAtUtc).ThenBy(a => a.Id).ToArrayAsync(ct);
            var deltas = new List<ProratedQuota>();
            foreach (var change in changes)
            {
                var previous = await VersionAsync(change.PreviousPlanVersionId, ct);
                var next = await VersionAsync(change.NextPlanVersionId, ct);
                var oldLimit = previous.QuotaDefinitions.SingleOrDefault(q => q.Key == key)?.Limit ?? 0;
                var newLimit = next.QuotaDefinitions.SingleOrDefault(q => q.Key == key)?.Limit ?? 0;
                deltas.Add(new ProratedQuota(newLimit - oldLimit, change.RemainingTicks, change.DurationTicks));
            }
            limit = CommercialProration.EffectiveLimit(initialQuota?.Limit ?? 0, deltas);
        }
        var consumed = QuotaKinds.For(key) == QuotaKind.CurrentCapacity
            ? await CapacityAsync(new SubscriptionOwner(subscription.OwnerType, subscription.OwnerId), key, ct)
            : await db.UsageLedgerEntries.AsNoTracking().Where(e => e.SubscriptionId == subscription.Id && e.QuotaKey == key && e.Status == UsageReservationStatus.Consumed)
                .SumAsync(e => (long?)e.Amount, ct) ?? 0;
        var reserved = await db.UsageReservations.AsNoTracking().Where(r => r.SubscriptionId == subscription.Id && r.QuotaKey == key
            && (r.Status == UsageReservationStatus.Reserved || r.Status == UsageReservationStatus.Unknown)).SumAsync(r => (long?)r.Amount, ct) ?? 0;
        var available = key is not (QuotaKey.SourceCount or QuotaKey.AssignmentFileBytes)
            && (key != QuotaKey.StorageBytes || subscription.OwnerType == SubscriptionOwnerType.Organization);
        return new CommercialQuotaDto(key, QuotaKinds.For(key), QuotaKinds.Unit(key), limit, consumed, reserved,
            available && limit.HasValue ? Math.Max(0, limit.Value - consumed - reserved) : null,
            limit.HasValue && (consumed > limit || reserved > limit.Value - consumed), version.WarningPercent)
            { CriticalWarningPercent = version.CriticalWarningPercent, IsUsageAvailable = available };
    }

    public async Task<long> CapacityAsync(SubscriptionOwner owner, QuotaKey key, CancellationToken ct)
    {
        if (owner.Type == SubscriptionOwnerType.Individual)
            return key switch
            {
                QuotaKey.ActiveClasses => await db.PersonalClasses.LongCountAsync(c => c.HostUserId == owner.Id && c.ArchivedAtUtc == null, ct),
                QuotaKey.ActiveLearners => await (from p in db.PersonalClassParticipants join c in db.PersonalClasses on p.PersonalClassId equals c.Id
                    where c.HostUserId == owner.Id && c.ArchivedAtUtc == null && p.EndedAtUtc == null select p.StudentUserId).Distinct().LongCountAsync(ct),
                _ => 0
            };
        return key switch
        {
            QuotaKey.ActiveClasses => await db.Classes.LongCountAsync(c => c.OrganizationId == owner.Id && c.Status == ClassStatus.Active && c.EndedAtUtc == null, ct),
            QuotaKey.ActiveLearners => await (from e in db.Enrollments join c in db.Classes on e.ClassId equals c.Id
                where e.OrganizationId == owner.Id && e.Status == EnrollmentStatus.Active && e.EndedAtUtc == null
                    && c.Status == ClassStatus.Active && c.EndedAtUtc == null select e.LearnerOrganizationPersonId).Distinct().LongCountAsync(ct),
            QuotaKey.StorageBytes => await db.FileAssets.Where(f => f.OrganizationId == owner.Id && f.Status == FileAssetStatus.Ready).SumAsync(f => (long?)f.SizeBytes, ct) ?? 0,
            _ => 0
        };
    }

    public async Task RequireCapacityAsync(SubscriptionOwner owner, EntitlementKey feature, QuotaKey key, long amount, DateTime utcNow, CancellationToken ct)
    {
        var subscription = await CurrentAsync(owner, utcNow, ct) ?? throw Error(CommerceErrorCodes.SubscriptionInactive);
        var version = await VersionAsync(subscription.PlanVersionId, ct);
        if (!version.Entitlements.Any(e => e.Key == feature && e.Enabled)) throw Error(CommerceErrorCodes.FeatureNotEntitled);
        var quota = await QuotaAsync(subscription, version, key, ct);
        if (!quota.IsUsageAvailable) throw Error(CommerceErrorCodes.MeterUnavailable);
        if (quota.Remaining.HasValue && amount > quota.Remaining) throw Error(CommerceErrorCodes.QuotaExceeded);
    }

    public async Task<CommercialSummaryDto> ScheduleDowngradeAsync(SubscriptionOwner owner, ScheduleDowngradeCommand command,
        Guid actorUserId, Guid sessionId, DateTime utcNow, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockOwnerAsync(owner, ct);
        var current = await CurrentAsync(owner, utcNow, ct) ?? throw Error(CommerceErrorCodes.SubscriptionInactive);
        if (await db.Subscriptions.AnyAsync(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id && s.Status == SubscriptionStatus.Scheduled
            && s.StartsAtUtc >= current.EndsAtUtc, ct)) throw Error(CommerceErrorCodes.UsageConflict);
        var tracked = await db.Subscriptions.SingleAsync(s => s.Id == current.Id, ct);
        byte[] expected;
        try { expected = Convert.FromBase64String(command.ExpectedVersion); } catch (FormatException) { throw Error(CommerceErrorCodes.UsageInvalid); }
        if (!tracked.RowVersion.SequenceEqual(expected)) throw Error(CommerceErrorCodes.UsageConflict);
        var next = await VersionAsync(command.PlanVersionId, ct);
        var plan = await db.Plans.SingleAsync(p => p.Id == next.PlanId, ct);
        var previous = await VersionAsync(current.PlanVersionId, ct);
        if (plan.Audience != owner.Type || next.Calendar != previous.Calendar || !(next.PlanId == previous.PlanId ? plan.RenewalAvailable : plan.NewPurchaseAvailable)
            || next.ValidatePublication().Count != 0) throw Error(CommerceErrorCodes.PlanNotPurchasable);
        tracked.ScheduleDowngrade(next, utcNow);
        db.AdminAuditLogs.Add(Audit(actorUserId, sessionId, "commerce.subscription.schedule-downgrade", tracked.Id, owner, command.Reason,
            current.PlanVersionId.ToString(), next.Id.ToString(), utcNow));
        realtime.CaptureCommerce();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await SummaryAsync(owner, utcNow, ct);
    }

    internal static AdminAuditLog Audit(Guid actor, Guid session, string action, Guid target, SubscriptionOwner owner,
        string reason, string? before, string? after, DateTime utcNow, string targetType = "Subscription") => AdminAuditLog.Create(actor, null, session, action,
            targetType, target.ToString(), owner.Type == SubscriptionOwnerType.Organization ? owner.Id : null,
            reason, utcNow, Guid.NewGuid().ToString("N"), AdminAuditResult.Succeeded, before, after);
    internal static CommerceException Error(string code) => new(code, code switch
    {
        CommerceErrorCodes.FeatureNotEntitled => "قرارداد، حق استفاده از این قابلیت را ندارد.",
        CommerceErrorCodes.QuotaExceeded => "سهمیهٔ منبع انتخاب‌شده کافی نیست.",
        CommerceErrorCodes.CommercialLocked => "مدیریت آموزشی سازمان تا تمدید قرارداد قفل است.",
        CommerceErrorCodes.SubscriptionInactive => "قرارداد معتبر فعلی وجود ندارد.",
        _ => "عملیات تجاری با وضعیت یا ورودی معتبر سازگار نیست."
    });
    internal static CommerceException Denied() => Error(CommerceErrorCodes.ContextNotAllowed);
}
