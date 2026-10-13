using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Platform;

public sealed class PlatformOperationsService(
    IPlatformOperationsStore store,
    IPlatformStepUpAuthorization stepUpAuthorization,
    TimeProvider timeProvider) : IPlatformOperationsService
{
    public Task<PlatformOrganizationPage> GetOrganizationsAsync(
        PlatformOrganizationQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        if (query.Search?.Trim().Length > 200 ||
            !IsSort(query.SortBy, "name", "createdAtUtc", "status") ||
            !IsDirection(query.SortDirection))
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "پارامترهای فهرست سازمان معتبر نیستند.");
        }

        return store.GetOrganizationsAsync(
            query with { Search = NormalizeOptional(query.Search) },
            cancellationToken);
    }

    public async Task<PlatformOrganizationDetail> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await store.GetOrganizationAsync(RequireId(organizationId), cancellationToken)
        ?? throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");

    public async Task<PlatformOrganizationImpactPreview> GetOrganizationImpactAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await store.GetOrganizationImpactAsync(RequireId(organizationId), cancellationToken)
        ?? throw Error(PlatformErrorCodes.OrganizationNotFound, "سازمان پیدا نشد.");

    public Task<PlatformProvisioningPage> GetProvisioningAsync(
        PlatformProvisioningQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        return store.GetProvisioningAsync(query, cancellationToken);
    }

    public Task<PlatformProvisioningResult> CreateOrganizationAsync(
        Guid actorUserId,
        Guid actorSessionId,
        CreatePlatformOrganizationCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        ProvisionCoreAsync(
            actorUserId,
            actorSessionId,
            new ProvisionOrganizationCommand(
                command.ClientOperationId,
                command.Name,
                null,
                null,
                null,
                null,
                command.Reason),
            correlationId,
            requireStepUp: false,
            cancellationToken);

    public Task<PlatformProvisioningResult> ProvisionAsync(
        Guid actorUserId,
        Guid actorSessionId,
        ProvisionOrganizationCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        ProvisionCoreAsync(
            actorUserId,
            actorSessionId,
            command,
            correlationId,
            requireStepUp: command.PlanVersionId.HasValue,
            cancellationToken);

    public Task<PlatformOrganizationMutationResult> SuspendOrganizationAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid organizationId,
        PlatformOrganizationStateCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        SetOrganizationStateAsync(actorUserId, actorSessionId, organizationId, false, command.Reason, correlationId, cancellationToken);

    public Task<PlatformOrganizationMutationResult> ReactivateOrganizationAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid organizationId,
        PlatformOrganizationStateCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        SetOrganizationStateAsync(actorUserId, actorSessionId, organizationId, true, command.Reason, correlationId, cancellationToken);

    public Task<PlatformMembershipPage> GetMembershipsAsync(
        Guid organizationId,
        PlatformMembershipQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        return store.GetMembershipsAsync(RequireId(organizationId), query, cancellationToken);
    }

    public Task<PlatformInvitationResult> CreateInvitationAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid organizationId,
        CreatePlatformInvitationCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        if (command.UserId == Guid.Empty || !Enum.IsDefined(command.Role) ||
            command.ExpiresAtUtc.Kind != DateTimeKind.Utc || command.ExpiresAtUtc <= UtcNow())
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "مشخصات دعوت معتبر نیست.");
        }

        return store.CreateInvitationAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(organizationId),
            command with { Reason = command.Reason.Trim() },
            cancellationToken);
    }

    public Task<PlatformMembershipMutationResult> ChangeMembershipRoleAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid organizationId,
        Guid membershipId,
        ChangePlatformMembershipRoleCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        if (!Enum.IsDefined(command.FromRole) || !Enum.IsDefined(command.ToRole) || command.FromRole == command.ToRole)
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "تغییر نقش معتبر نیست.");
        }

        return store.ChangeMembershipRoleAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(organizationId),
            RequireId(membershipId),
            command with { Reason = command.Reason.Trim() },
            cancellationToken);
    }

    public Task<PlatformMembershipMutationResult> EndMembershipAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid organizationId,
        Guid membershipId,
        EndPlatformMembershipCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        return store.EndMembershipAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(organizationId),
            RequireId(membershipId),
            command.Reason.Trim(),
            cancellationToken);
    }

    public Task<PlatformUserPage> GetUsersAsync(
        PlatformUserQuery query,
        CancellationToken cancellationToken)
    {
        ValidatePage(query.Page, query.PageSize);
        if (query.Search?.Trim().Length > 200 ||
            !IsSort(query.SortBy, "displayName", "createdAtUtc", "status") ||
            !IsDirection(query.SortDirection))
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "پارامترهای فهرست کاربر معتبر نیستند.");
        }

        return store.GetUsersAsync(query with { Search = NormalizeOptional(query.Search) }, cancellationToken);
    }

    public async Task<PlatformUserDetail> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await store.GetUserAsync(RequireId(userId), cancellationToken)
        ?? throw Error(PlatformErrorCodes.UserNotFound, "کاربر پیدا نشد.");

    public Task<PlatformUserMutationResult> SuspendUserAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid userId,
        PlatformUserStateCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        SetUserStateAsync(actorUserId, actorSessionId, userId, false, command.Reason, correlationId, cancellationToken);

    public Task<PlatformUserMutationResult> ReactivateUserAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid userId,
        PlatformUserStateCommand command,
        string correlationId,
        CancellationToken cancellationToken) =>
        SetUserStateAsync(actorUserId, actorSessionId, userId, true, command.Reason, correlationId, cancellationToken);

    public Task<PlatformSessionRevocationResult> RevokeSessionsAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid userId,
        Guid? sessionId,
        RevokePlatformSessionsCommand command,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        if (sessionId == Guid.Empty)
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "شناسه نشست معتبر نیست.");
        }

        return store.RevokeSessionsAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(userId),
            sessionId,
            command.Reason.Trim(),
            cancellationToken);
    }

    private async Task<PlatformProvisioningResult> ProvisionCoreAsync(
        Guid actorUserId,
        Guid actorSessionId,
        ProvisionOrganizationCommand command,
        string correlationId,
        bool requireStepUp,
        CancellationToken cancellationToken)
    {
        ValidateReason(command.Reason);
        var nowUtc = UtcNow();
        if (string.IsNullOrWhiteSpace(command.ClientOperationId) || command.ClientOperationId.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(command.OrganizationName) || command.OrganizationName.Trim().Length > 200 ||
            command.PlanVersionId == Guid.Empty || command.InitialManagerUserId == Guid.Empty ||
            command.SubscriptionEndsAtUtc is { Kind: not DateTimeKind.Utc } ||
            command.InvitationExpiresAtUtc is { Kind: not DateTimeKind.Utc } ||
            command.SubscriptionEndsAtUtc.HasValue &&
                (!command.PlanVersionId.HasValue || command.SubscriptionEndsAtUtc.Value <= nowUtc) ||
            command.InvitationExpiresAtUtc.HasValue &&
                (!command.InitialManagerUserId.HasValue || command.InvitationExpiresAtUtc.Value <= nowUtc) ||
            command.InitialManagerUserId.HasValue && !command.InvitationExpiresAtUtc.HasValue)
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "درخواست provisioning معتبر نیست.");
        }

        if (requireStepUp)
        {
            await EnsureStepUpAsync(actorUserId, actorSessionId, cancellationToken);
        }

        return await store.ProvisionAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            command with
            {
                ClientOperationId = command.ClientOperationId.Trim(),
                OrganizationName = command.OrganizationName.Trim(),
                Reason = command.Reason.Trim()
            },
            cancellationToken);
    }

    private async Task<PlatformOrganizationMutationResult> SetOrganizationStateAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid organizationId,
        bool active,
        string reason,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        await EnsureStepUpAsync(actorUserId, actorSessionId, cancellationToken);
        return await store.SetOrganizationActiveAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(organizationId),
            active,
            reason.Trim(),
            cancellationToken);
    }

    private async Task<PlatformUserMutationResult> SetUserStateAsync(
        Guid actorUserId,
        Guid actorSessionId,
        Guid userId,
        bool active,
        string reason,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        await EnsureStepUpAsync(actorUserId, actorSessionId, cancellationToken);
        return await store.SetUserActiveAsync(
            Actor(actorUserId, actorSessionId, correlationId),
            RequireId(userId),
            active,
            reason.Trim(),
            cancellationToken);
    }

    private async Task EnsureStepUpAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        if (!await stepUpAuthorization.IsSatisfiedAsync(userId, sessionId, cancellationToken))
        {
            throw Error(PlatformErrorCodes.StepUpRequired, "برای این عملیات احراز هویت تقویت‌شده لازم است.");
        }
    }

    private PlatformActorContext Actor(Guid userId, Guid sessionId, string correlationId)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty || string.IsNullOrWhiteSpace(correlationId))
        {
            throw Error(PlatformErrorCodes.AccessDenied, "هویت عامل عملیات معتبر نیست.");
        }

        return new PlatformActorContext(userId, sessionId, UtcNow(), correlationId.Trim());
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static Guid RequireId(Guid id) =>
        id != Guid.Empty ? id : throw Error(PlatformErrorCodes.QueryInvalid, "شناسه معتبر نیست.");

    private static void ValidatePage(int page, int pageSize)
    {
        if (page is <= 0 or > 100_000 || pageSize is <= 0 or > 100)
        {
            throw Error(PlatformErrorCodes.QueryInvalid, "صفحه‌بندی معتبر نیست.");
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
        {
            throw Error(PlatformErrorCodes.ReasonRequired, "دلیل معتبر و حداکثر ۵۰۰ کاراکتر الزامی است.");
        }
    }

    private static bool IsSort(string value, params string[] allowed) =>
        allowed.Contains(value?.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool IsDirection(string value) =>
        value?.Trim().Equals("asc", StringComparison.OrdinalIgnoreCase) == true ||
        value?.Trim().Equals("desc", StringComparison.OrdinalIgnoreCase) == true;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PlatformException Error(string code, string message) => new(code, message);
}
