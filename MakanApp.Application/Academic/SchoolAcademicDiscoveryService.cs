using MakanApp.Application.Assessment;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Academic;

public sealed record TeacherContentActivityQuery(Guid? TeacherUserId = null, Guid? ClassId = null,
    AcademicActivityKind? Kind = null, DateTime? FromUtc = null, DateTime? ToUtc = null, int Page = 1, int PageSize = 50);
public sealed record TeacherContentActivityDto(Guid? EventId, Guid TeacherUserId, string TeacherDisplayName, Guid ClassId,
    string ClassTitle, AcademicActivityKind Kind, Guid SourceVersionId, int SourceRevision, string Title, DateTime PublishedAtUtc);
public sealed record OrganizationExamQuery(Guid? ClassId = null, ExamVersionStatus? Status = null,
    string? Search = null, int Page = 1, int PageSize = 25);
public interface ISchoolAcademicDiscoveryStore
{
    Task<SchoolPage<TeacherContentActivityDto>> ActivitiesAsync(AccessContext context, TeacherContentActivityQuery query, DateTime now, CancellationToken ct);
    Task<AcademicPage<ExamSummaryRecord>> ExamsAsync(AccessContext context, OrganizationExamQuery query, CancellationToken ct);
}
public sealed class SchoolAcademicDiscoveryService(SchoolManagementAccessResolver managers, IAccessContextResolver contexts,
    ISchoolAcademicDiscoveryStore store, TimeProvider clock)
{
    public async Task<SchoolPage<TeacherContentActivityDto>> ActivitiesAsync(Guid userId, Guid sessionId, TeacherContentActivityQuery query, CancellationToken ct)
    {
        var context = await managers.ResolveAsync(userId, sessionId, ct);
        SchoolOnboardingValidation.Page(query.Page, query.PageSize); SchoolQueryValidation.EnumValue(query.Kind);
        if (query.TeacherUserId == Guid.Empty || query.ClassId == Guid.Empty || query.FromUtc?.Kind is DateTimeKind.Local or DateTimeKind.Unspecified ||
            query.ToUtc?.Kind is DateTimeKind.Local or DateTimeKind.Unspecified || query.FromUtc > query.ToUtc ||
            query.Kind == AcademicActivityKind.Session)
            throw SchoolOnboardingValidation.Invalid("فیلتر نوع محتوا یا بازهٔ UTC معتبر نیست.");
        return await store.ActivitiesAsync(context, query, clock.GetUtcNow().UtcDateTime, ct);
    }
    public async Task<AcademicPage<StudentExamSummary>> ExamsAsync(Guid userId, Guid sessionId, OrganizationExamQuery query, CancellationToken ct)
    {
        var context = await contexts.ResolveAsync(userId, sessionId, ct);
        if (context.WorkspaceType != WorkspaceType.Organization || context.OrganizationId is null ||
            context.ActiveRole is not (OrganizationRole.Manager or OrganizationRole.Teacher))
            throw new AcademicException(AcademicErrorCodes.AcademicReadNotAllowed, "فضای جاری مدرس یا مدیر سازمان لازم است.");
        SchoolOnboardingValidation.Page(query.Page, query.PageSize); SchoolQueryValidation.EnumValue(query.Status);
        if (query.ClassId == Guid.Empty || query.Search?.Length > 200) throw SchoolOnboardingValidation.Invalid("فیلتر آزمون معتبر نیست.");
        var page = await store.ExamsAsync(context, query with { Search = query.Search?.Trim() }, ct);
        return new(page.Items.Select(ExamService.ToSummary).ToArray(), page.Page, page.PageSize, page.Total);
    }
}
