namespace MakanApp.Application.Platform;

public static class PlatformErrorCodes
{
    public const string AccessDenied = "PLATFORM_ACCESS_DENIED";
    public const string UserNotFound = "PLATFORM_USER_NOT_FOUND";
    public const string RoleNotFound = "PLATFORM_ROLE_NOT_FOUND";
    public const string AssignmentNotFound = "PLATFORM_ASSIGNMENT_NOT_FOUND";
    public const string AssignmentConflict = "PLATFORM_ASSIGNMENT_CONFLICT";
    public const string LastOwnerRequired = "PLATFORM_LAST_OWNER_REQUIRED";
    public const string BootstrapClosed = "PLATFORM_BOOTSTRAP_CLOSED";
    public const string StepUpRequired = "PLATFORM_STEP_UP_REQUIRED";
    public const string ReasonRequired = "PLATFORM_REASON_REQUIRED";
    public const string QueryInvalid = "PLATFORM_QUERY_INVALID";
    public const string OrganizationNotFound = "PLATFORM_ORGANIZATION_NOT_FOUND";
    public const string OrganizationConflict = "PLATFORM_ORGANIZATION_CONFLICT";
    public const string ProvisioningNotFound = "PLATFORM_PROVISIONING_NOT_FOUND";
    public const string ProvisioningConflict = "PLATFORM_PROVISIONING_CONFLICT";
    public const string PlanVersionNotPublished = "PLATFORM_PLAN_VERSION_NOT_PUBLISHED";
    public const string MembershipNotFound = "PLATFORM_MEMBERSHIP_NOT_FOUND";
    public const string MembershipConflict = "PLATFORM_MEMBERSHIP_CONFLICT";
    public const string LastManagerRequired = "PLATFORM_LAST_MANAGER_REQUIRED";
    public const string AccountSuspended = "PLATFORM_ACCOUNT_SUSPENDED";
    public const string SessionNotFound = "PLATFORM_SESSION_NOT_FOUND";
    public const string EducationResourceNotFound = "PLATFORM_EDUCATION_RESOURCE_NOT_FOUND";
    public const string ModerationCaseNotFound = "PLATFORM_MODERATION_CASE_NOT_FOUND";
    public const string ModerationStateConflict = "PLATFORM_MODERATION_STATE_CONFLICT";
}
