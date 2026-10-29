using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Commerce;

public sealed record CommercialAvailabilityCommand(bool CatalogVisible, bool NewPurchaseAvailable, bool RenewalAvailable, string ExpectedVersion, string Reason);
public sealed record CommercialDraftCommand(decimal? PriceToman, bool ExplicitlyFree, BillingCalendar? Calendar, string? SaleTermsReference,
    int? WarningPercent, int? CriticalWarningPercent, IReadOnlyCollection<PlatformPlanEntitlementItem> Entitlements,
    IReadOnlyCollection<PlatformPlanQuotaItem> Quotas, string ExpectedVersion, string Reason);
public sealed record RegisterCommercialContractCommand(Guid PlanVersionId, DateTime BillingAnchorUtc, int BillingPeriodIndex, string? ExpectedCurrentVersion, string Reason);
public sealed record CommercialValidationDto(Guid PlanVersionId, IReadOnlyList<string> MissingFields, bool CanPublish, string Version);
public sealed record CommercialHistoryDto(Guid Id, Guid? ActorUserId, string Action, string TargetId, DateTime OccurredAtUtc,
    string Reason, string? Before, string? After);

public interface ICommercialAdminStore
{
    Task SetAvailabilityAsync(Guid actor, Guid session, Guid planId, CommercialAvailabilityCommand command, DateTime now, CancellationToken ct);
    Task<CommercialValidationDto> UpdateDraftAsync(Guid actor, Guid session, Guid versionId, CommercialDraftCommand command, DateTime now, CancellationToken ct);
    Task<CommercialValidationDto> ValidateAsync(Guid versionId, CancellationToken ct);
    Task<CommercialSummaryDto> RegisterAsync(Guid actor, Guid session, SubscriptionOwner owner, RegisterCommercialContractCommand command, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<CommercialHistoryDto>> HistoryAsync(Guid targetId, CancellationToken ct);
}

public sealed class CommercialAdminService(CommercialActorAccess actors, IPlatformPermissionResolver permissions, ICommercialAdminStore store,
    ICommercialStore reads, TimeProvider timeProvider)
{
    public async Task SetAvailabilityAsync(Guid actor, Guid session, Guid id, CommercialAvailabilityCommand command, CancellationToken ct)
    { await Authorize(actor, session, true, ct); Reason(command.Reason); await store.SetAvailabilityAsync(actor, session, id, command, Now(), ct); }
    public async Task<CommercialValidationDto> UpdateDraftAsync(Guid actor, Guid session, Guid id, CommercialDraftCommand command, CancellationToken ct)
    {
        await Authorize(actor, session, true, ct); Reason(command.Reason);
        if (command.PriceToman < 0 || command.PriceToman.HasValue && command.PriceToman > PlanVersionPrice.MaximumAmount / 10m
            || command.ExplicitlyFree && command.PriceToman is not (null or 0)
            || command.PriceToman == 0 && !command.ExplicitlyFree || command.Entitlements is null || command.Quotas is null
            || command.Entitlements.Select(e => e.Key).Distinct().Count() != command.Entitlements.Count
            || command.Quotas.Select(q => q.Key).Distinct().Count() != command.Quotas.Count) throw CommercialSubscriptionService.Invalid();
        return await store.UpdateDraftAsync(actor, session, id, command, Now(), ct);
    }
    public async Task<CommercialValidationDto> ValidateAsync(Guid actor, Guid session, Guid id, CancellationToken ct)
    { await Authorize(actor, session, false, ct); return await store.ValidateAsync(id, ct); }
    public async Task<CommercialSummaryDto> RegisterAsync(Guid actor, Guid session, SubscriptionOwnerType type, Guid ownerId,
        RegisterCommercialContractCommand command, CancellationToken ct)
    {
        await Authorize(actor, session, true, ct); Reason(command.Reason);
        if (!Enum.IsDefined(type) || ownerId == Guid.Empty || command.PlanVersionId == Guid.Empty || command.BillingAnchorUtc.Kind != DateTimeKind.Utc
            || command.BillingPeriodIndex < 0) throw CommercialSubscriptionService.Invalid();
        return await store.RegisterAsync(actor, session, new SubscriptionOwner(type, ownerId), command, Now(), ct);
    }
    public async Task<IReadOnlyList<CommercialHistoryDto>> HistoryAsync(Guid actor, Guid session, Guid id, CancellationToken ct)
    { await Authorize(actor, session, false, ct); return await store.HistoryAsync(id, ct); }
    public async Task<CommercialSummaryDto> SummaryAsync(Guid actor, Guid session, SubscriptionOwnerType type, Guid ownerId, CancellationToken ct)
    {
        await Authorize(actor, session, false, ct);
        if (!Enum.IsDefined(type) || ownerId == Guid.Empty) throw CommercialSubscriptionService.Invalid();
        return await reads.SummaryAsync(new SubscriptionOwner(type, ownerId), Now(), ct);
    }
    private async Task Authorize(Guid actor, Guid session, bool write, CancellationToken ct)
    {
        await actors.EnsureAsync(actor, session, ct);
        if (!await permissions.HasPermissionAsync(actor, write ? PlatformPermissionKeys.CommerceManage : PlatformPermissionKeys.CommerceRead, ct))
            throw CommercialSubscriptionService.Denied();
    }
    private static void Reason(string value)
    { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 500) throw CommercialSubscriptionService.Invalid(); }
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
