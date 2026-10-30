using System.Data;
using System.Security.Cryptography;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfCommercialStore
{
    public async Task<AiQuotaPreflightDto> PreflightAsync(CommercialContext context, DateTime utcNow, CancellationToken ct)
    {
        if (context.Trial)
        {
            var (start, end) = CommercialPeriods.TrialDay(utcNow);
            var consumed = await db.UsageLedgerEntries.Where(e => e.SubscriptionId == null && e.OwnerType == SubscriptionOwnerType.Individual
                && e.OwnerId == context.ConsumerUserId && e.PeriodStartsAtUtc == start && e.QuotaKey == QuotaKey.AiTextRequests
                && e.Status == UsageReservationStatus.Consumed).SumAsync(e => (long?)e.Amount, ct) ?? 0;
            var reserved = await db.UsageReservations.Where(r => r.IsTrial && r.ConsumerUserId == context.ConsumerUserId
                && r.PeriodStartsAtUtc == start && (r.Status == UsageReservationStatus.Reserved || r.Status == UsageReservationStatus.Unknown))
                .SumAsync(r => (long?)r.Amount, ct) ?? 0;
            var remaining = Math.Max(0, 3 - consumed - reserved);
            return new AiQuotaPreflightDto(AiPayerSelection.FreeTrial, context.Owner.Type, context.Owner.Id, "simple_text_request", 1,
                remaining, end, remaining > 0, remaining > 0 ? null : CommerceErrorCodes.QuotaExceeded, false);
        }
        var subscription = await CurrentAsync(context.Owner, utcNow, ct);
        var payer = context.Owner.Type == SubscriptionOwnerType.Organization ? AiPayerSelection.Organization : AiPayerSelection.Individual;
        if (subscription is null) return new AiQuotaPreflightDto(payer, context.Owner.Type, context.Owner.Id, "simple_text_request", 1,
            0, null, false, CommerceErrorCodes.SubscriptionInactive, false);
        var version = await VersionAsync(subscription.PlanVersionId, ct);
        if (!version.Entitlements.Any(e => e.Key == EntitlementKey.Ai && e.Enabled))
            return new AiQuotaPreflightDto(payer, context.Owner.Type, context.Owner.Id, "simple_text_request", 1,
                0, subscription.EndsAtUtc, false, CommerceErrorCodes.FeatureNotEntitled, false);
        if (!version.QuotaDefinitions.Any(q => q.Key == QuotaKey.AiTextRequests))
            return new AiQuotaPreflightDto(payer, context.Owner.Type, context.Owner.Id, "simple_text_request", 1,
                0, subscription.EndsAtUtc, false, CommerceErrorCodes.QuotaRequired, false);
        var quota = await QuotaAsync(subscription, version, QuotaKey.AiTextRequests, ct);
        return new AiQuotaPreflightDto(payer, context.Owner.Type, context.Owner.Id, "simple_text_request", 1,
            quota.Remaining, subscription.EndsAtUtc, !quota.Remaining.HasValue || quota.Remaining > 0,
            quota.Remaining == 0 ? CommerceErrorCodes.QuotaExceeded : null, false);
    }

    public async Task<UsageReservationDto> ReserveAsync(CommercialContext context, Guid operationId, byte[] hash, DateTime utcNow, CancellationToken ct)
        => await ReserveCoreAsync(context, QuotaKey.AiTextRequests, EntitlementKey.Ai, operationId, hash, utcNow, ct);

    public async Task<UsageReservationDto?> ReserveReportExportAsync(Guid organizationId, Guid consumerUserId, Guid jobId, byte[] hash,
        DateTime utcNow, CancellationToken ct)
    {
        if (!await db.Subscriptions.AnyAsync(s => s.OrganizationId == organizationId && s.CommercialControlEnabled, ct)) return null;
        return await ReserveCoreAsync(new CommercialContext(consumerUserId, SubscriptionOwner.Organization(organizationId),
            CommerceContextType.Organization, organizationId, false), QuotaKey.ReportExports, EntitlementKey.Reports, jobId, hash, utcNow, ct);
    }

    private async Task<UsageReservationDto> ReserveCoreAsync(CommercialContext context, QuotaKey key, EntitlementKey feature,
        Guid operationId, byte[] hash, DateTime utcNow, CancellationToken ct)
    {
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await LockOwnerAsync(SubscriptionOwner.Individual(context.ConsumerUserId), ct);
        if (context.Owner != SubscriptionOwner.Individual(context.ConsumerUserId)) await LockOwnerAsync(context.Owner, ct);
        var existing = await db.UsageReservations.AsNoTracking().SingleOrDefaultAsync(r => r.ConsumerUserId == context.ConsumerUserId && r.OperationId == operationId, ct);
        if (existing is not null)
        {
            if (existing.OwnerType != context.Owner.Type || existing.OwnerId != context.Owner.Id || existing.ContextType != context.ContextType
                || existing.ContextId != context.ContextId || existing.QuotaKey != key || existing.IsTrial != context.Trial || !CryptographicOperations.FixedTimeEquals(existing.PayloadHash, hash))
                throw Error(CommerceErrorCodes.UsageConflict);
            if (tx is not null) await tx.CommitAsync(ct); return Map(existing, true);
        }
        if (key == QuotaKey.AiTextRequests)
        {
            var preflight = await PreflightAsync(context, utcNow, ct);
            if (!preflight.Allowed) throw Error(preflight.ErrorCode!);
        }
        else await RequireCapacityAsync(context.Owner, feature, key, 1, utcNow, ct);
        var subscription = context.Trial ? null : await CurrentAsync(context.Owner, utcNow, ct);
        var period = context.Trial ? CommercialPeriods.TrialDay(utcNow) : (subscription!.StartsAtUtc,
            subscription.EndsAtUtc ?? throw Error(CommerceErrorCodes.TermsIncomplete));
        var reservation = UsageReservation.Create(context.Owner, subscription?.Id, subscription?.PlanVersionId,
            context.ConsumerUserId, context.ContextType, context.ContextId, key, period.Item1, period.Item2,
            1, operationId, hash, context.Trial, utcNow);
        db.UsageReservations.Add(reservation);
        db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(reservation, utcNow, "reserved"));
        realtime.CaptureCommerce();
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return Map(reservation, false);
    }

    public async Task<UsageReservationDto> FinalizeAsync(Guid consumerUserId, Guid reservationId, UsageReservationStatus status,
        string reasonCode, DateTime utcNow, CancellationToken ct)
    {
        await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        await LockOwnerAsync(SubscriptionOwner.Individual(consumerUserId), ct, requireActive: false);
        var reservation = await db.UsageReservations.SingleOrDefaultAsync(r => r.Id == reservationId && r.ConsumerUserId == consumerUserId, ct)
            ?? throw Error(CommerceErrorCodes.ResourceNotFound);
        await db.Entry(reservation).ReloadAsync(ct);
        if (reservation.OwnerType == SubscriptionOwnerType.Organization)
            await LockOwnerAsync(SubscriptionOwner.Organization(reservation.OwnerId), ct);
        bool changed;
        try { changed = reservation.Finalize(status, reasonCode, utcNow); }
        catch (ArgumentException) { throw Error(CommerceErrorCodes.UsageInvalid); }
        catch (InvalidOperationException) { throw Error(CommerceErrorCodes.UsageConflict); }
        if (changed) db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(reservation, utcNow, reasonCode));
        realtime.CaptureCommerce();
        await db.SaveChangesAsync(ct); if (tx is not null) await tx.CommitAsync(ct); return Map(reservation, !changed);
    }

    private static UsageReservationDto Map(UsageReservation r, bool replayed) => new(r.Id, r.OperationId, r.OwnerType, r.OwnerId,
        r.SubscriptionId, r.PlanVersionId, r.QuotaKey, r.Amount, r.PeriodStartsAtUtc, r.PeriodEndsAtUtc, r.Status, replayed);
}
