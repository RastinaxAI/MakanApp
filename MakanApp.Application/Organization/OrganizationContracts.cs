using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed record WorkspaceResult(
    WorkspaceType WorkspaceType,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? MembershipId,
    OrganizationRole? Role,
    string DisplayName,
    string Status);

public sealed record SelectWorkspaceCommand(
    WorkspaceType WorkspaceType,
    Guid? MembershipId,
    OrganizationRole? Role);

public sealed record AccessContext(
    Guid ActorId,
    Guid UserId,
    Guid SessionId,
    WorkspaceType WorkspaceType,
    Guid? OrganizationId,
    Guid? MembershipId,
    OrganizationRole? ActiveRole,
    Guid? SubjectOrganizationPersonId);

public sealed record InvitationResult(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    Guid InvitedByUserId,
    OrganizationRole Role,
    InvitationStatus Status,
    DateTime ExpiresAtUtc,
    Guid? OrganizationPersonId = null,
    string? OrganizationPersonDisplayName = null,
    Guid? GuardianLearnerOrganizationPersonId = null,
    string? GuardianLearnerDisplayName = null)
{
    public bool RequiresPersonLinkConsent => OrganizationPersonId.HasValue;
    public bool RequiresGuardianConsent => GuardianLearnerOrganizationPersonId.HasValue;
    public string? NextAction => Status == InvitationStatus.Pending && RequiresGuardianConsent ? "CONFIRM_GUARDIAN_RELATION" : null;
}

public sealed record AcceptInvitationCommand(bool ConfirmPersonLink = false, bool ConfirmGuardianRelation = false);

public sealed record AcceptInvitationResult(
    Guid InvitationId,
    Guid OrganizationId,
    string OrganizationName,
    Guid MembershipId,
    Guid RoleAssignmentId,
    OrganizationRole Role,
    bool AlreadyAccepted,
    Guid? OrganizationPersonId = null,
    Guid? GuardianRelationId = null);

public sealed record DeclineInvitationResult(
    Guid InvitationId,
    InvitationStatus Status);

public sealed record OrganizationWorkspaceRecord(
    Guid OrganizationId,
    string OrganizationName,
    Guid MembershipId,
    OrganizationRole Role);

public sealed record InvitationWithOrganization(
    Invitation Invitation,
    string OrganizationName,
    string? OrganizationPersonDisplayName = null,
    string? GuardianLearnerDisplayName = null);
