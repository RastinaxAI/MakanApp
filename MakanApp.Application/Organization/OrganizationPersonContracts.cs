using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed record CreateOrganizationPersonCommand(
    Guid ClientOperationId, string FirstName, string LastName, string? DisplayName, string Reason);
public sealed record UpdateOrganizationPersonNamesCommand(
    string FirstName, string LastName, string? DisplayName,
    string ExpectedRowVersion, string ExpectedPersonRowVersion, string Reason);
public sealed record OnboardingStateCommand(string ExpectedRowVersion, string Reason);
public sealed record CreatePersonLinkInvitationCommand(
    Guid ClientOperationId, Guid DestinationUserId, DateTime ExpiresAtUtc, string ExpectedRowVersion, string Reason);
public sealed record OrganizationPersonDto(
    Guid Id, Guid OrganizationId, Guid PersonId, string FirstName, string LastName, string? DisplayName,
    OrganizationPersonStatus Status, bool IsAccountLinked, bool CanEditNames,
    string RowVersion, string PersonRowVersion, bool Replayed = false);
public sealed record OrganizationPeoplePage(
    DateTime DataAsOfUtc, int Page, int PageSize, int TotalCount, IReadOnlyCollection<OrganizationPersonDto> Items);
public sealed record PersonLinkInvitationDto(
    Guid Id, Guid OrganizationPersonId, Guid DestinationUserId, OrganizationRole Role,
    InvitationStatus Status, DateTime ExpiresAtUtc, string RowVersion, bool Replayed = false);
public sealed record CreateSchoolGuardianRelationCommand(
    Guid GuardianUserId, Guid LearnerOrganizationPersonId, string Reason);
public sealed record SchoolGuardianRelationDto(
    Guid Id, Guid OrganizationId, Guid GuardianUserId, Guid LearnerOrganizationPersonId,
    GuardianRelationStatus Status, DateTime ValidFromUtc, DateTime? EndedAtUtc,
    string RowVersion, bool Replayed = false);
public sealed record SchoolGuardianRelationsPage(
    DateTime DataAsOfUtc, int Page, int PageSize, int TotalCount, IReadOnlyCollection<SchoolGuardianRelationDto> Items);

public static class SchoolOnboardingErrorCodes
{
    public const string Invalid = "ORGANIZATION_ONBOARDING_INVALID";
    public const string PersonNotFound = "ORGANIZATION_PERSON_NOT_FOUND";
    public const string NamesProtected = "ORGANIZATION_PERSON_NAMES_PROTECTED";
    public const string Conflict = "ORGANIZATION_ONBOARDING_CONFLICT";
    public const string AccountLinkConflict = "ORGANIZATION_PERSON_ACCOUNT_LINK_CONFLICT";
    public const string AccountProfileRequired = "ACCOUNT_PROFILE_REQUIRED";
    public const string ConsentRequired = "PERSON_LINK_CONSENT_REQUIRED";
    public const string PersonInUse = "ORGANIZATION_PERSON_IN_USE";
    public const string GuardianNotFound = "SCHOOL_GUARDIAN_RELATION_NOT_FOUND";
    public const string GuardianStateConflict = "SCHOOL_GUARDIAN_RELATION_STATE_CONFLICT";
}
