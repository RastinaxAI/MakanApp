using MakanApp.Domain.Academic;

namespace MakanApp.Application.Assessment;

public sealed record AcademicPage<T>(IReadOnlyCollection<T> Items, int Page, int PageSize, int Total)
{
    // نام قبلی برای مصرف‌کنندگان فعلی حفظ می‌شود؛ قرارداد مشترک جدید از TotalCount استفاده می‌کند.
    public int TotalCount => Total;
}
public sealed record AcademicListQuery(int Page = 1, int PageSize = 25, string? Search = null, bool? Published = null);

public interface IPersonalAcademicReadStore
{
    Task<AcademicPage<AssignmentWithVersionRecord>> AssignmentsAsync(AcademicAccessContext access, AcademicListQuery query, CancellationToken ct);
    Task<AcademicPage<ExamSummaryRecord>> ExamsAsync(AcademicAccessContext access, AcademicListQuery query, CancellationToken ct);
}

public sealed class PersonalAcademicReadService(IAcademicContextResolver contexts, IPersonalAcademicReadStore store)
{
    public async Task<AcademicPage<AssignmentResult>> AssignmentsAsync(Guid userId, Guid sessionId, Guid classId, AcademicListQuery query, CancellationToken ct)
    {
        Validate(query);
        var access = await contexts.ResolveAsync(userId, sessionId, AcademicResourceKind.PersonalClass, classId, AcademicOperation.Read, false, ct);
        if (!access.IsLocalLearner && !access.Has(PersonalClassAcademicCapabilities.AuthorAssignments) && !access.Has(PersonalClassAcademicCapabilities.Grade)) throw Denied();
        var page = await store.AssignmentsAsync(access, query, ct);
        return new(page.Items.Select(r => AssignmentService.ToResult(r.Assignment, r.Version, r.ClassTitle)).ToArray(), page.Page, page.PageSize, page.Total);
    }

    public async Task<AcademicPage<StudentExamSummary>> ExamsAsync(Guid userId, Guid sessionId, Guid classId, AcademicListQuery query, CancellationToken ct)
    {
        Validate(query);
        var access = await contexts.ResolveAsync(userId, sessionId, AcademicResourceKind.PersonalClass, classId, AcademicOperation.Read, false, ct);
        if (!access.IsLocalLearner && !access.Has(PersonalClassAcademicCapabilities.AuthorExams) && !access.Has(PersonalClassAcademicCapabilities.Grade)) throw Denied();
        var page = await store.ExamsAsync(access, query, ct);
        return new(page.Items.Select(ExamService.ToSummary).ToArray(), page.Page, page.PageSize, page.Total);
    }

    internal static void Validate(AcademicListQuery query)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 100 || query.Search?.Length > 200)
            throw new AssessmentException(AssessmentErrorCodes.AcademicQueryInvalid, "پارامتر صفحه‌بندی یا جستجو معتبر نیست.");
    }
    private static AssessmentException Denied() => new(AssessmentErrorCodes.AssignmentNotFound, "کلاس آموزشی در دسترس نیست.");
}
