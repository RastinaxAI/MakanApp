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
}
