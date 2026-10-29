using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public sealed class SchoolReadService(SchoolManagementAccessResolver managerAccess, IAccessContextResolver access,
    ISchoolReadStore store, TimeProvider clock)
{
    public async Task<SchoolPage<SchoolMemberResult>> MembersAsync(Guid userId, Guid sessionId, SchoolMemberQuery query, CancellationToken ct)
    {
        var context = await managerAccess.ResolveAsync(userId, sessionId, ct);
        var page = SchoolQueryValidation.Normalize(new(query.Search, query.Page, query.PageSize));
        SchoolQueryValidation.EnumValue(query.Role); SchoolQueryValidation.EnumValue(query.Status);
        return await store.GetMembersAsync(context, query with { Search = page.Search }, Now(), ct);
    }

    public async Task<SchoolPage<ClassLearnerResult>> LearnersAsync(Guid userId, Guid sessionId, Guid classId, SchoolPageQuery query, EnrollmentStatus? status, CancellationToken ct)
    {
        var context = await RequireTeachingClassAsync(userId, sessionId, classId, ct);
        SchoolQueryValidation.EnumValue(status);
        return await store.GetLearnersAsync(context, classId, SchoolQueryValidation.Normalize(query), status, Now(), ct);
    }

    public async Task<SchoolPage<ClassTeacherResult>> TeachersAsync(Guid userId, Guid sessionId, Guid classId, SchoolPageQuery query, TeacherAssignmentStatus? status, CancellationToken ct)
    {
        var context = await RequireTeachingClassAsync(userId, sessionId, classId, ct);
        SchoolQueryValidation.EnumValue(status);
        return await store.GetTeachersAsync(context, classId, SchoolQueryValidation.Normalize(query), status, Now(), ct);
    }

    public async Task<SchoolPage<CourseResult>> CoursesAsync(Guid userId, Guid sessionId, SchoolPageQuery query, CourseStatus? status, CancellationToken ct)
    {
        var context = await managerAccess.ResolveAsync(userId, sessionId, ct);
        SchoolQueryValidation.EnumValue(status);
        return await store.GetCoursesAsync(context.OrganizationId!.Value, SchoolQueryValidation.Normalize(query), status, Now(), ct);
    }

    public async Task<SchoolPage<AcademicPeriodResult>> PeriodsAsync(Guid userId, Guid sessionId, SchoolPageQuery query, AcademicPeriodStatus? status, CancellationToken ct)
    {
        var context = await managerAccess.ResolveAsync(userId, sessionId, ct);
        SchoolQueryValidation.EnumValue(status);
        return await store.GetPeriodsAsync(context.OrganizationId!.Value, SchoolQueryValidation.Normalize(query), status, Now(), ct);
    }

    public async Task<AccessContext> RequireTeachingClassAsync(Guid userId, Guid sessionId, Guid classId, CancellationToken ct)
    {
        var context = await access.ResolveAsync(userId, sessionId, ct);
        if (classId == Guid.Empty || !await store.CanManageClassAsync(context, classId, ct))
            throw new AcademicException(AcademicErrorCodes.ClassNotFound, "کلاس مجاز پیدا نشد.");
        return context;
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
