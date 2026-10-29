using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Academic;

public sealed record SchoolPage<T>(DateTime DataAsOfUtc, int Page, int PageSize, int TotalCount, IReadOnlyCollection<T> Items);
public sealed record SchoolPageQuery(string? Search = null, int Page = 1, int PageSize = 50);
public sealed record SchoolMemberQuery(string? Search = null, OrganizationRole? Role = null, MembershipStatus? Status = null, int Page = 1, int PageSize = 50);
public sealed record SchoolRoleAssignmentResult(Guid RoleAssignmentId, OrganizationRole Role, RoleAssignmentStatus Status, DateTime AssignedAtUtc, DateTime? EndedAtUtc, string Version);
public sealed record SchoolMemberResult(Guid UserId, Guid? PersonId, Guid? OrganizationPersonId, string DisplayName, string? Username,
    ProfilePhotoResult? ProfilePhoto, Guid MembershipId, MembershipStatus MembershipStatus,
    IReadOnlyCollection<OrganizationRole> ActiveRoles, IReadOnlyCollection<SchoolRoleAssignmentResult> RoleAssignments, string Version);
public sealed record ClassLearnerResult(Guid EnrollmentId, Guid LearnerOrganizationPersonId, Guid PersonId, Guid? UserId,
    string DisplayName, string? Username, ProfilePhotoResult? ProfilePhoto, EnrollmentStatus Status, DateTime EnrolledAtUtc, DateTime? EndedAtUtc, string Version);
public sealed record ClassTeacherResult(Guid TeacherAssignmentId, Guid TeacherMembershipId, Guid UserId, Guid? PersonId,
    string DisplayName, string? Username, ProfilePhotoResult? ProfilePhoto, TeacherAssignmentStatus Status, DateTime AssignedAtUtc, DateTime? EndedAtUtc, string Version);

public interface ISchoolReadStore
{
    Task<bool> CanManageOrganizationAsync(AccessContext context, CancellationToken ct);
    Task<SchoolPage<SchoolMemberResult>> GetMembersAsync(AccessContext context, SchoolMemberQuery query, DateTime now, CancellationToken ct);
    Task<bool> CanManageClassAsync(AccessContext context, Guid classId, CancellationToken ct);
    Task<bool> CanStudyClassAsync(AccessContext context, Guid classId, CancellationToken ct);
    Task<SchoolPage<ClassLearnerResult>> GetLearnersAsync(AccessContext context, Guid classId, SchoolPageQuery query, EnrollmentStatus? status, DateTime now, CancellationToken ct);
    Task<SchoolPage<ClassTeacherResult>> GetTeachersAsync(AccessContext context, Guid classId, SchoolPageQuery query, TeacherAssignmentStatus? status, DateTime now, CancellationToken ct);
    Task<SchoolPage<CourseResult>> GetCoursesAsync(Guid organizationId, SchoolPageQuery query, CourseStatus? status, DateTime now, CancellationToken ct);
    Task<SchoolPage<AcademicPeriodResult>> GetPeriodsAsync(Guid organizationId, SchoolPageQuery query, AcademicPeriodStatus? status, DateTime now, CancellationToken ct);
}

public static class SchoolErrorCodes
{
    public const string Invalid = "SCHOOL_REQUEST_INVALID";
    public const string LessonNotFound = "LESSON_NOT_FOUND";
    public const string LessonConflict = "LESSON_STATE_CONFLICT";
    public const string LessonVersionConflict = "LESSON_VERSION_CONFLICT";
    public const string GuardianTargetUnavailable = "GUARDIAN_INVITATION_TARGET_NOT_AVAILABLE";
    public const string GuardianConsentRequired = "GUARDIAN_INVITATION_CONSENT_REQUIRED";
    public const string GuardianInvitationConflict = "GUARDIAN_INVITATION_CONFLICT";
}

public static class SchoolQueryValidation
{
    public static SchoolPageQuery Normalize(SchoolPageQuery query)
    {
        var search = query.Search?.Trim();
        if (query.Page < 1 || query.Page > 100_000 || query.PageSize is < 1 or > 100 || search?.Length > 100)
            throw new AcademicException(SchoolErrorCodes.Invalid, "صفحه، اندازهٔ ۱ تا ۱۰۰ و جست‌وجوی حداکثر ۱۰۰ نویسه لازم است.");
        return query with { Search = search };
    }

    public static void EnumValue<T>(T? value) where T : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(value.Value))
            throw new AcademicException(SchoolErrorCodes.Invalid, "مقدار فیلتر معتبر نیست.");
    }
}
