using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Commerce;

public sealed class CommercialSubscriptionService(IAccessContextResolver accessResolver, CommercialActorAccess actors, ICommercialStore store, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<CommercialPlanDto>> CatalogAsync(Guid userId, Guid sessionId, SubscriptionOwnerType audience, CancellationToken cancellationToken)
    {
        await actors.EnsureAsync(userId, sessionId, cancellationToken);
        if (!Enum.IsDefined(audience)) throw Invalid();
        return await store.CatalogAsync(audience, Now(), cancellationToken);
    }

    public async Task<CommercialSummaryDto> SummaryAsync(Guid userId, Guid sessionId, SubscriptionOwnerType type, CancellationToken cancellationToken) =>
        await store.SummaryAsync(await ResolveOwnerAsync(userId, sessionId, type, cancellationToken), Now(), cancellationToken);

    public async Task<SubscriptionOwner> ResolveOwnerAsync(Guid userId, Guid sessionId, SubscriptionOwnerType type, CancellationToken cancellationToken)
    {
        await actors.EnsureAsync(userId, sessionId, cancellationToken);
        if (type == SubscriptionOwnerType.Individual) return SubscriptionOwner.Individual(userId);
        var context = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (type != SubscriptionOwnerType.Organization || context.OrganizationId is null || context.MembershipId is null
            || context.WorkspaceType != WorkspaceType.Organization || context.ActiveRole != OrganizationRole.Manager)
            throw Denied();
        return SubscriptionOwner.Organization(context.OrganizationId.Value);
    }

    public async Task<AiQuotaPreflightDto> PreflightAsync(Guid userId, Guid sessionId, AiQuotaPreflightCommand command, CancellationToken cancellationToken)
    {
        var context = await ResolveContextAsync(userId, sessionId, command, cancellationToken);
        var result = await store.PreflightAsync(context, Now(), cancellationToken);
        return result with { RequiresExplicitSelection = command.Payer is null && command.ContextType == CommerceContextType.Organization };
    }

    public async Task<UsageReservationDto> ReserveSimpleTextAsync(Guid userId, Guid sessionId, ReserveUsageCommand command, CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty || command.PayloadHash is not { Length: 32 }) throw Invalid();
        var context = await ResolveContextAsync(userId, sessionId, command.Context, cancellationToken);
        return await store.ReserveAsync(context, command.OperationId, command.PayloadHash, Now(), cancellationToken);
    }

    // این port داخلی فقط نتیجهٔ قطعی مجری را ثبت می‌کند و endpoint عمومی ندارد.
    public Task<UsageReservationDto> FinalizeAsync(Guid consumerUserId, Guid reservationId, UsageReservationStatus status,
        string reasonCode, CancellationToken cancellationToken) =>
        store.FinalizeAsync(consumerUserId, reservationId, status, reasonCode, Now(), cancellationToken);

    public async Task<CommercialSummaryDto> ScheduleDowngradeAsync(Guid userId, Guid sessionId, SubscriptionOwnerType type,
        ScheduleDowngradeCommand command, CancellationToken cancellationToken)
    {
        if (command.PlanVersionId == Guid.Empty || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500)
            throw Invalid();
        return await store.ScheduleDowngradeAsync(await ResolveOwnerAsync(userId, sessionId, type, cancellationToken),
            command, userId, sessionId, Now(), cancellationToken);
    }

    private async Task<CommercialContext> ResolveContextAsync(Guid userId, Guid sessionId, AiQuotaPreflightCommand command, CancellationToken cancellationToken)
    {
        await actors.EnsureAsync(userId, sessionId, cancellationToken);
        if (!Enum.IsDefined(command.ContextType) || command.Payer.HasValue && !Enum.IsDefined(command.Payer.Value)) throw Invalid();
        Guid contextId;
        if (command.ContextType == CommerceContextType.Organization)
        {
            var actor = await accessResolver.ResolveAsync(userId, sessionId, cancellationToken);
            if (actor.WorkspaceType != WorkspaceType.Organization || actor.OrganizationId is null || actor.MembershipId is null
                || actor.OrganizationId != command.ContextId) throw Denied();
            contextId = actor.OrganizationId.Value;
        }
        else if (command.ContextType == CommerceContextType.PersonalClass)
        {
            if (command.ContextId is null || !await store.CanAccessPersonalClassAsync(userId, command.ContextId.Value, cancellationToken))
                throw Denied();
            contextId = command.ContextId.Value;
        }
        else
        {
            if (command.ContextId.HasValue && command.ContextId != userId) throw Denied();
            contextId = userId;
        }
        var payer = command.Payer ?? (command.ContextType == CommerceContextType.Organization ? AiPayerSelection.Organization : AiPayerSelection.Individual);
        if (payer == AiPayerSelection.Organization && command.ContextType != CommerceContextType.Organization) throw Denied();
        var owner = payer == AiPayerSelection.Organization ? SubscriptionOwner.Organization(contextId) : SubscriptionOwner.Individual(userId);
        return new CommercialContext(userId, owner, command.ContextType, contextId, payer == AiPayerSelection.FreeTrial);
    }

    internal static CommerceException Invalid() => new(CommerceErrorCodes.UsageInvalid, "درخواست تجاری معتبر نیست.");
    internal static CommerceException Denied() => new(CommerceErrorCodes.ContextNotAllowed, "زمینهٔ مجاز برای این عملیات وجود ندارد.");
    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
