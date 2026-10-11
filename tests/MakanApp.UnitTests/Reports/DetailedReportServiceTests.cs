using System.Text.Json;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Reports;

public sealed class DetailedReportServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 11, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ValidStudentQueryUsesNormalizedPaginationAndSingleServerTimestamp()
    {
        var context = Context(OrganizationRole.Student);
        var reader = new RecordingReader();
        var service = Service(context, reader);

        var result = await service.GetStudentAttendanceAsync(
            context.UserId,
            context.SessionId,
            new DetailedReportQuery { PageNumber = 2, PageSize = 25, Sort = "OccurredAsc" },
            CancellationToken.None);

        Assert.Equal(NowUtc, result.DataAsOfUtc);
        Assert.Equal(context, reader.LastContext);
        Assert.Equal(new ReportQuerySpec(2, 25, null, null, null, "occurredasc"), reader.LastQuery);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task InvalidPaginationIsRejectedBeforeReader(int pageNumber, int pageSize)
    {
        var context = Context(OrganizationRole.Student);
        var reader = new RecordingReader();
        var service = Service(context, reader);

        var exception = await Assert.ThrowsAsync<DetailedReportException>(() =>
            service.GetStudentAttendanceAsync(context.UserId, context.SessionId, new DetailedReportQuery { PageNumber = pageNumber, PageSize = pageSize }, CancellationToken.None));

        Assert.Equal(DetailedReportErrorCodes.PaginationInvalid, exception.Code);
        Assert.Null(reader.LastContext);
    }

    [Fact]
    public async Task InvalidUtcRangeAndUnknownSortAreRejected()
    {
        var context = Context(OrganizationRole.Student);
        var service = Service(context, new RecordingReader());
        var range = await Assert.ThrowsAsync<DetailedReportException>(() => service.GetStudentAttendanceAsync(context.UserId, context.SessionId, new DetailedReportQuery { FromUtc = NowUtc, ToUtc = NowUtc.AddMinutes(-1) }, CancellationToken.None));
        var sort = await Assert.ThrowsAsync<DetailedReportException>(() => service.GetStudentAttendanceAsync(context.UserId, context.SessionId, new DetailedReportQuery { Sort = "sqlExpression" }, CancellationToken.None));

        Assert.Equal(DetailedReportErrorCodes.DateRangeInvalid, range.Code);
        Assert.Equal(DetailedReportErrorCodes.SortInvalid, sort.Code);
    }

    [Fact]
    public async Task ParentReportRequiresSelectedChildAndTeacherClassIsConcealed()
    {
        var parent = Context(OrganizationRole.Parent) with { SubjectOrganizationPersonId = null };
        var parentService = Service(parent, new RecordingReader());
        var parentError = await Assert.ThrowsAsync<GuardianException>(() => parentService.GetParentExamsAsync(parent.UserId, parent.SessionId, new DetailedReportQuery(), CancellationToken.None));

        var teacher = Context(OrganizationRole.Teacher);
        var teacherService = Service(teacher, new RecordingReader { TeacherAccess = false });
        var teacherError = await Assert.ThrowsAsync<DetailedReportException>(() => teacherService.GetTeacherAttendanceAsync(teacher.UserId, teacher.SessionId, Guid.NewGuid(), new DetailedReportQuery(), CancellationToken.None));

        Assert.Equal(GuardianErrorCodes.ChildContextNotFound, parentError.Code);
        Assert.Equal(DetailedReportErrorCodes.ResourceNotFound, teacherError.Code);
    }

    [Fact]
    public async Task WrongRoleCannotCallAnotherAudienceReport()
    {
        var context = Context(OrganizationRole.Manager);
        var service = Service(context, new RecordingReader());

        var exception = await Assert.ThrowsAsync<OrganizationException>(() => service.GetStudentGradesAsync(context.UserId, context.SessionId, new DetailedReportQuery(), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.WorkspaceNotAllowed, exception.Code);
    }

    [Fact]
    public void PublicReportContractsDoNotExposeSensitiveFields()
    {
        var json = JsonSerializer.Serialize(new object[]
        {
            new StudentExamReportItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "کلاس", "آزمون", NowUtc, NowUtc.AddHours(1), 30, 1, 20, 0, null, null, null, null, null, null),
            new ParentAssignmentReportItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "کلاس", "تکلیف", Domain.Assessment.AssignmentStatus.Published, NowUtc, 20, null, null, null, null, null, null),
            new ManagerTeacherWorkloadReportItem(Guid.NewGuid(), Guid.NewGuid(), "معلم", 1, 2, 0, 1, 1)
        });

        foreach (var forbidden in new[] { "AnswerKey", "CorrectOption", "TeacherPrivateNote", "EvaluatorPrivateNote", "Message", "SessionToken", "RowVersion" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static DetailedReportService Service(AccessContext context, RecordingReader reader) =>
        new(new Resolver(context), reader, new FixedClock());

    private static AccessContext Context(OrganizationRole role) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkspaceType.Organization, Guid.NewGuid(), Guid.NewGuid(), role, role == OrganizationRole.Parent ? Guid.NewGuid() : null);

    private sealed class Resolver(AccessContext context) : IAccessContextResolver
    {
        public Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) => Task.FromResult(context);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(NowUtc);
    }

    private sealed class RecordingReader : IDetailedReportReadService
    {
        public AccessContext? LastContext { get; private set; }
        public ReportQuerySpec? LastQuery { get; private set; }
        public bool TeacherAccess { get; init; } = true;

        public Task<AttendanceReportPage<StudentAttendanceReportItem>> ReadStudentAttendanceAsync(AccessContext context, DateTime at, ReportQuerySpec query, CancellationToken token)
        {
            LastContext = context;
            LastQuery = query;
            return Task.FromResult(new AttendanceReportPage<StudentAttendanceReportItem>(at, query.PageNumber, query.PageSize, 0, EmptyAttendance, []));
        }

        public Task<bool> HasActiveTeacherAssignmentAsync(AccessContext context, Guid classId, CancellationToken token) => Task.FromResult(TeacherAccess);
        public Task<ReportPage<StudentAssignmentReportItem>> ReadStudentAssignmentsAsync(AccessContext c, DateTime d, ReportQuerySpec q, CancellationToken t) => Empty<StudentAssignmentReportItem>(d, q);
        public Task<ReportPage<StudentExamReportItem>> ReadStudentExamsAsync(AccessContext c, DateTime d, ReportQuerySpec q, CancellationToken t) => Empty<StudentExamReportItem>(d, q);
        public Task<ReportPage<StudentReleasedGradeReportItem>> ReadStudentGradesAsync(AccessContext c, DateTime d, ReportQuerySpec q, CancellationToken t) => Empty<StudentReleasedGradeReportItem>(d, q);
        public Task<AttendanceReportPage<ParentAttendanceReportItem>> ReadParentAttendanceAsync(AccessContext c, DateTime d, ReportQuerySpec q, CancellationToken t) => EmptyAttendancePage<ParentAttendanceReportItem>(d, q);
        public Task<ReportPage<ParentAssignmentReportItem>> ReadParentAssignmentsAsync(AccessContext c, DateTime d, ReportQuerySpec q, CancellationToken t) => Empty<ParentAssignmentReportItem>(d, q);
        public Task<ReportPage<ParentExamReportItem>> ReadParentExamsAsync(AccessContext c, DateTime d, ReportQuerySpec q, CancellationToken t) => Empty<ParentExamReportItem>(d, q);
        public Task<AttendanceReportPage<TeacherAttendanceReportItem>> ReadTeacherAttendanceAsync(AccessContext c, DateTime d, Guid id, ReportQuerySpec q, CancellationToken t) => EmptyAttendancePage<TeacherAttendanceReportItem>(d, q);
        public Task<ReportPage<TeacherAssignmentReportItem>> ReadTeacherAssignmentsAsync(AccessContext c, DateTime d, Guid id, ReportQuerySpec q, CancellationToken t) => Empty<TeacherAssignmentReportItem>(d, q);
        public Task<ReportPage<TeacherExamReportItem>> ReadTeacherExamsAsync(AccessContext c, DateTime d, Guid id, ReportQuerySpec q, CancellationToken t) => Empty<TeacherExamReportItem>(d, q);
        public Task<TeacherLearnerOverviewDto?> ReadTeacherLearnerAsync(AccessContext c, DateTime d, Guid classId, Guid learnerId, CancellationToken t) => Task.FromResult<TeacherLearnerOverviewDto?>(null);
        public Task<ReportPage<ManagerEnrollmentReportItem>> ReadManagerEnrollmentsAsync(AccessContext c, DateTime d, ManagerEnrollmentReportQuery f, ReportQuerySpec q, CancellationToken t) => Empty<ManagerEnrollmentReportItem>(d, q);
        public Task<ReportPage<ManagerAttendanceReportItem>> ReadManagerAttendanceAsync(AccessContext c, DateTime d, ReportQuerySpec q, CancellationToken t) => Empty<ManagerAttendanceReportItem>(d, q);
        public Task<ReportPage<ManagerAssessmentReportItem>> ReadManagerAssessmentsAsync(AccessContext c, DateTime d, ManagerAssessmentReportQuery f, ReportQuerySpec q, CancellationToken t) => Empty<ManagerAssessmentReportItem>(d, q);
        public Task<ManagerClassPerformanceDto?> ReadManagerClassAsync(AccessContext c, DateTime d, Guid id, CancellationToken t) => Task.FromResult<ManagerClassPerformanceDto?>(null);
        public Task<ReportPage<ManagerTeacherWorkloadReportItem>> ReadManagerTeacherWorkloadAsync(AccessContext c, DateTime d, TeacherWorkloadReportQuery f, ReportQuerySpec q, CancellationToken t) => Empty<ManagerTeacherWorkloadReportItem>(d, q);

        private static Task<ReportPage<T>> Empty<T>(DateTime dataAsOfUtc, ReportQuerySpec query) => Task.FromResult(new ReportPage<T>(dataAsOfUtc, query.PageNumber, query.PageSize, 0, []));
        private static AttendanceReportSummary EmptyAttendance { get; } = new(0, 0, 0, 0, 0, 0, 0);
        private static Task<AttendanceReportPage<T>> EmptyAttendancePage<T>(DateTime dataAsOfUtc, ReportQuerySpec query) => Task.FromResult(new AttendanceReportPage<T>(dataAsOfUtc, query.PageNumber, query.PageSize, 0, EmptyAttendance, []));
    }
}
