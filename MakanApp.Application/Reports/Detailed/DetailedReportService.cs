using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Reports.Detailed;

public sealed class DetailedReportService(
    IAccessContextResolver accessContextResolver,
    IDetailedReportReadService readService,
    TimeProvider timeProvider) : IDetailedReportService
{
    private const int MaximumPageSize = 100;

    private static readonly string[] AttendanceSorts = ["occurreddesc", "occurredasc"];
    private static readonly string[] AssignmentSorts = ["duedesc", "dueasc", "titleasc"];
    private static readonly string[] ExamSorts = ["availabledesc", "availableasc", "titleasc"];
    private static readonly string[] GradeSorts = ["releaseddesc", "releasedasc"];
    private static readonly string[] EnrollmentSorts = ["enrolleddesc", "enrolledasc", "learnerasc"];
    private static readonly string[] ManagerAttendanceSorts = ["classtitleasc", "eligibledesc", "absenceratedesc"];
    private static readonly string[] ManagerAssessmentSorts = ["deadlinedesc", "deadlineasc", "titleasc", "pendingdesc"];
    private static readonly string[] WorkloadSorts = ["teacherasc", "classesdesc", "pendingdesc"];

    public async Task<AttendanceReportPage<StudentAttendanceReportItem>> GetStudentAttendanceAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Student, cancellationToken);
        return await readService.ReadStudentAttendanceAsync(context, UtcNow(), Normalize(query, AttendanceSorts, "occurreddesc"), cancellationToken);
    }

    public Task<ReportPage<StudentAssignmentReportItem>> GetStudentAssignmentsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadAsync(userId, sessionId, OrganizationRole.Student, query, AssignmentSorts, "duedesc", readService.ReadStudentAssignmentsAsync, cancellationToken);

    public Task<ReportPage<StudentExamReportItem>> GetStudentExamsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadAsync(userId, sessionId, OrganizationRole.Student, query, ExamSorts, "availabledesc", readService.ReadStudentExamsAsync, cancellationToken);

    public Task<ReportPage<StudentReleasedGradeReportItem>> GetStudentGradesAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadAsync(userId, sessionId, OrganizationRole.Student, query, GradeSorts, "releaseddesc", readService.ReadStudentGradesAsync, cancellationToken);

    public async Task<AttendanceReportPage<ParentAttendanceReportItem>> GetParentAttendanceAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken)
    {
        var context = await ResolveParentAsync(userId, sessionId, cancellationToken);
        return await readService.ReadParentAttendanceAsync(context, UtcNow(), Normalize(query, AttendanceSorts, "occurreddesc"), cancellationToken);
    }

    public Task<ReportPage<ParentAssignmentReportItem>> GetParentAssignmentsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadParentAsync(userId, sessionId, query, AssignmentSorts, "duedesc", readService.ReadParentAssignmentsAsync, cancellationToken);

    public Task<ReportPage<ParentExamReportItem>> GetParentExamsAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadParentAsync(userId, sessionId, query, ExamSorts, "availabledesc", readService.ReadParentExamsAsync, cancellationToken);

    public async Task<AttendanceReportPage<TeacherAttendanceReportItem>> GetTeacherAttendanceAsync(Guid userId, Guid sessionId, Guid classId, DetailedReportQuery query, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Teacher, cancellationToken);
        await RequireTeacherClassAsync(context, classId, cancellationToken);
        return await readService.ReadTeacherAttendanceAsync(context, UtcNow(), classId, Normalize(query, AttendanceSorts, "occurreddesc"), cancellationToken);
    }

    public Task<ReportPage<TeacherAssignmentReportItem>> GetTeacherAssignmentsAsync(Guid userId, Guid sessionId, Guid classId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadTeacherAsync(userId, sessionId, classId, query, AssignmentSorts, "duedesc", readService.ReadTeacherAssignmentsAsync, cancellationToken);

    public Task<ReportPage<TeacherExamReportItem>> GetTeacherExamsAsync(Guid userId, Guid sessionId, Guid classId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadTeacherAsync(userId, sessionId, classId, query, ExamSorts, "availabledesc", readService.ReadTeacherExamsAsync, cancellationToken);

    public async Task<TeacherLearnerOverviewDto> GetTeacherLearnerAsync(Guid userId, Guid sessionId, Guid classId, Guid learnerId, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Teacher, cancellationToken);
        await RequireTeacherClassAsync(context, classId, cancellationToken);
        return await readService.ReadTeacherLearnerAsync(context, UtcNow(), classId, learnerId, cancellationToken)
            ?? throw NotFound();
    }

    public async Task<ReportPage<ManagerEnrollmentReportItem>> GetManagerEnrollmentsAsync(Guid userId, Guid sessionId, ManagerEnrollmentReportQuery query, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Manager, cancellationToken);
        return await readService.ReadManagerEnrollmentsAsync(context, UtcNow(), query, Normalize(query, EnrollmentSorts, "enrolleddesc"), cancellationToken);
    }

    public Task<ReportPage<ManagerAttendanceReportItem>> GetManagerAttendanceAsync(Guid userId, Guid sessionId, DetailedReportQuery query, CancellationToken cancellationToken) =>
        ReadAsync(userId, sessionId, OrganizationRole.Manager, query, ManagerAttendanceSorts, "classtitleasc", readService.ReadManagerAttendanceAsync, cancellationToken);

    public async Task<ReportPage<ManagerAssessmentReportItem>> GetManagerAssessmentsAsync(Guid userId, Guid sessionId, ManagerAssessmentReportQuery query, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Manager, cancellationToken);
        return await readService.ReadManagerAssessmentsAsync(context, UtcNow(), query, Normalize(query, ManagerAssessmentSorts, "deadlinedesc"), cancellationToken);
    }

    public async Task<ManagerClassPerformanceDto> GetManagerClassAsync(Guid userId, Guid sessionId, Guid classId, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Manager, cancellationToken);
        return await readService.ReadManagerClassAsync(context, UtcNow(), classId, cancellationToken)
            ?? throw NotFound();
    }

    public async Task<ReportPage<ManagerTeacherWorkloadReportItem>> GetManagerTeacherWorkloadAsync(Guid userId, Guid sessionId, TeacherWorkloadReportQuery query, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Manager, cancellationToken);
        return await readService.ReadManagerTeacherWorkloadAsync(context, UtcNow(), query, Normalize(query, WorkloadSorts, "teacherasc"), cancellationToken);
    }

    private async Task<ReportPage<T>> ReadAsync<T>(Guid userId, Guid sessionId, OrganizationRole role, DetailedReportQuery query, IReadOnlyCollection<string> sorts, string defaultSort, Func<AccessContext, DateTime, ReportQuerySpec, CancellationToken, Task<ReportPage<T>>> read, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, role, cancellationToken);
        return await read(context, UtcNow(), Normalize(query, sorts, defaultSort), cancellationToken);
    }

    private async Task<ReportPage<T>> ReadParentAsync<T>(Guid userId, Guid sessionId, DetailedReportQuery query, IReadOnlyCollection<string> sorts, string defaultSort, Func<AccessContext, DateTime, ReportQuerySpec, CancellationToken, Task<ReportPage<T>>> read, CancellationToken cancellationToken)
    {
        var context = await ResolveParentAsync(userId, sessionId, cancellationToken);
        return await read(context, UtcNow(), Normalize(query, sorts, defaultSort), cancellationToken);
    }

    private async Task<AccessContext> ResolveParentAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Parent, cancellationToken);
        if (!context.SubjectOrganizationPersonId.HasValue)
        {
            throw new GuardianException(GuardianErrorCodes.ChildContextNotFound, "برای مشاهده گزارش باید ابتدا فرزند را انتخاب کنید.");
        }

        return context;
    }

    private async Task<ReportPage<T>> ReadTeacherAsync<T>(Guid userId, Guid sessionId, Guid classId, DetailedReportQuery query, IReadOnlyCollection<string> sorts, string defaultSort, Func<AccessContext, DateTime, Guid, ReportQuerySpec, CancellationToken, Task<ReportPage<T>>> read, CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Teacher, cancellationToken);
        await RequireTeacherClassAsync(context, classId, cancellationToken);
        return await read(context, UtcNow(), classId, Normalize(query, sorts, defaultSort), cancellationToken);
    }

    private async Task RequireTeacherClassAsync(AccessContext context, Guid classId, CancellationToken cancellationToken)
    {
        if (classId == Guid.Empty || !await readService.HasActiveTeacherAssignmentAsync(context, classId, cancellationToken))
        {
            throw NotFound();
        }
    }

    private async Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, OrganizationRole requiredRole, CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization || !context.OrganizationId.HasValue || !context.MembershipId.HasValue || context.ActiveRole != requiredRole)
        {
            throw new OrganizationException(OrganizationErrorCodes.WorkspaceNotAllowed, "این گزارش فقط در فضای سازمانی و برای نقش فعال متناظر قابل مشاهده است.");
        }

        return context;
    }

    private static ReportQuerySpec Normalize(DetailedReportQuery query, IReadOnlyCollection<string> allowedSorts, string defaultSort)
    {
        if (query.PageNumber <= 0 || query.PageSize <= 0 || query.PageSize > MaximumPageSize)
        {
            throw new DetailedReportException(DetailedReportErrorCodes.PaginationInvalid, $"شماره صفحه باید مثبت و اندازه صفحه بین ۱ تا {MaximumPageSize} باشد.");
        }

        if ((query.FromUtc.HasValue && query.FromUtc.Value.Kind != DateTimeKind.Utc) ||
            (query.ToUtc.HasValue && query.ToUtc.Value.Kind != DateTimeKind.Utc) ||
            (query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc > query.ToUtc))
        {
            throw new DetailedReportException(DetailedReportErrorCodes.DateRangeInvalid, "بازه زمانی باید UTC باشد و زمان شروع از زمان پایان بیشتر نباشد.");
        }

        var sort = string.IsNullOrWhiteSpace(query.Sort) ? defaultSort : query.Sort.Trim().ToLowerInvariant();
        if (!allowedSorts.Contains(sort, StringComparer.Ordinal))
        {
            throw new DetailedReportException(DetailedReportErrorCodes.SortInvalid, "مرتب‌سازی درخواست‌شده برای این گزارش مجاز نیست.");
        }

        return new ReportQuerySpec(query.PageNumber, query.PageSize, query.FromUtc, query.ToUtc, query.ClassId, sort);
    }

    private static DetailedReportException NotFound() =>
        new(DetailedReportErrorCodes.ResourceNotFound, "گزارش یا منبع در محدوده دسترسی فعلی پیدا نشد.");

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
