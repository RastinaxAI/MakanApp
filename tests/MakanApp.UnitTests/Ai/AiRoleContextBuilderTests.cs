using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiRoleContextBuilderTests
{
    private static readonly DateTime NowUtc = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StudentContext_UsesOrganizationPersonAndReleasedScoreWithVersionMaxScore()
    {
        var learnerId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var assessmentId = Guid.NewGuid();
        var context = OrganizationContext(OrganizationRole.Student);
        var reports = new FakeReports
        {
            StudentAssignments = Page(new StudentAssignmentReportItem(
                assessmentId, Guid.NewGuid(), classId, "ریاضی", "تمرین", AssignmentStatus.Published,
                NowUtc.AddDays(2), 20m, null, null, null, null, null, null)),
            StudentGrades = Page(new StudentReleasedGradeReportItem(
                DetailedReportGradeKind.Assignment, assessmentId, classId, "ریاضی", "تمرین",
                17.25m, 20m, 86.25m, NowUtc.AddDays(-1)))
        };
        var builder = new StudentContextBuilder(
            new FakeResolver(context),
            new FakeScopeStore
            {
                LearnerId = learnerId,
                LearnerClasses = [new AiLearnerClassScope(classId, "ریاضی", "ریاضی")]
            },
            reports,
            new FixedTimeProvider(NowUtc));

        var result = await builder.BuildAsync(
            context.UserId, context.SessionId, CancellationToken.None);

        Assert.Equal(learnerId.ToString("D"), result.Scope.StudentId);
        var grade = Assert.Single(result.Data.Grades);
        Assert.Equal(17.25m, grade["score"].GetDecimal());
        Assert.Equal(20m, grade["maxScore"].GetDecimal());
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contact", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conversation", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("moderation", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StudentContext_WithoutOwnOrganizationPerson_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Student);
        var builder = new StudentContextBuilder(
            new FakeResolver(context),
            new FakeScopeStore(),
            new FakeReports(),
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId, context.SessionId, CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    [Fact]
    public async Task StudentContext_WithoutActiveEnrollment_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Student);
        var builder = new StudentContextBuilder(
            new FakeResolver(context),
            new FakeScopeStore { LearnerId = Guid.NewGuid(), LearnerClasses = [] },
            new FakeReports(),
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId, context.SessionId, CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    [Fact]
    public async Task ParentContext_RevokedRelationRejectedByResolver_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Parent, Guid.NewGuid());
        var resolver = new FakeResolver(context)
        {
            Error = new AiServiceException(AiServiceErrorCodes.ContextNotAllowed, "revoked")
        };
        var builder = new ParentChildContextBuilder(
            resolver,
            new FakeReports(),
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId, context.SessionId, CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    [Fact]
    public async Task TeacherClass_UnassignedClass_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Teacher);
        var builder = new TeacherClassContextBuilder(
            new FakeResolver(context),
            new FakeReports { HasTeacherAssignment = false },
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId, context.SessionId, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    [Fact]
    public async Task TeacherStudent_OutsideAssignedClass_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Teacher);
        var builder = new TeacherStudentContextBuilder(
            new FakeResolver(context),
            new FakeScopeStore
            {
                LearnerClass = new AiLearnerClassScope(Guid.NewGuid(), "کلاس", "درس")
            },
            new FakeReports { HasTeacherAssignment = true, TeacherLearner = null },
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId,
            context.SessionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    [Fact]
    public async Task ManagerClass_CrossOrganizationClass_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Manager);
        var builder = new ManagerClassContextBuilder(
            new FakeResolver(context),
            new FakeReports { ManagerClass = null },
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId, context.SessionId, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    [Fact]
    public async Task ManagerStudent_CrossOrganizationStudent_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Manager);
        var builder = new ManagerStudentContextBuilder(
            new FakeResolver(context),
            new FakeScopeStore { LearnerClass = null },
            new FakeReports(),
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId,
            context.SessionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    [Fact]
    public async Task PlatformOwnerWithoutManagerMembership_IsDenied()
    {
        var context = new AccessContext(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkspaceType.Personal,
            null, null, null, null);
        var builder = new ManagerSchoolContextBuilder(
            new FakeResolver(context),
            new FakeDashboards(),
            new FixedTimeProvider(NowUtc));

        var error = await Assert.ThrowsAsync<AiServiceException>(() => builder.BuildAsync(
            context.UserId, context.SessionId, CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
    }

    private static AccessContext OrganizationContext(
        OrganizationRole role,
        Guid? subjectId = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        WorkspaceType.Organization,
        Guid.NewGuid(),
        Guid.NewGuid(),
        role,
        subjectId);

    private static ReportPage<T> Page<T>(params T[] items) =>
        new(NowUtc, 1, Math.Max(1, items.Length), items.Length, items);

    private static AttendanceReportPage<T> AttendancePage<T>(params T[] items) =>
        new(NowUtc, 1, Math.Max(1, items.Length), items.Length,
            new AttendanceReportSummary(items.Length, items.Length, items.Length, 0, 0, 0, 0), items);

    private sealed class FakeResolver(AccessContext context) : IAccessContextResolver
    {
        public Exception? Error { get; init; }

        public Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
            Error is null ? Task.FromResult(context) : Task.FromException<AccessContext>(Error);
    }

    private sealed class FakeScopeStore : IAiScopeStore
    {
        public Guid? LearnerId { get; init; }
        public AiSelectedHomework? Homework { get; init; }
        public AiSelectedExam? Exam { get; init; }
        public AiLearnerClassScope? LearnerClass { get; init; }
        public IReadOnlyCollection<AiLearnerClassScope> LearnerClasses { get; init; } = [];

        public Task<Guid?> GetActiveLearnerOrganizationPersonIdAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(LearnerId);
        public Task<AiSelectedHomework?> GetPublishedHomeworkAsync(Guid organizationId, Guid learnerOrganizationPersonId, Guid assignmentId, CancellationToken cancellationToken) => Task.FromResult(Homework);
        public Task<AiSelectedExam?> GetPublishedExamAsync(Guid organizationId, Guid learnerOrganizationPersonId, Guid examId, CancellationToken cancellationToken) => Task.FromResult(Exam);
        public Task<AiLearnerClassScope?> GetActiveLearnerClassAsync(Guid organizationId, Guid learnerOrganizationPersonId, Guid classId, CancellationToken cancellationToken) => Task.FromResult(LearnerClass);
        public Task<IReadOnlyCollection<AiLearnerClassScope>> GetActiveLearnerClassesAsync(Guid organizationId, Guid learnerOrganizationPersonId, int maximumCount, CancellationToken cancellationToken) => Task.FromResult(LearnerClasses);
    }

    private sealed class FakeReports : IDetailedReportReadService
    {
        public bool HasTeacherAssignment { get; init; }
        public TeacherLearnerOverviewDto? TeacherLearner { get; init; }
        public ManagerClassPerformanceDto? ManagerClass { get; init; }
        public ReportPage<StudentAssignmentReportItem> StudentAssignments { get; init; } = Page<StudentAssignmentReportItem>();
        public ReportPage<StudentReleasedGradeReportItem> StudentGrades { get; init; } = Page<StudentReleasedGradeReportItem>();

        public Task<AttendanceReportPage<StudentAttendanceReportItem>> ReadStudentAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(AttendancePage<StudentAttendanceReportItem>());
        public Task<ReportPage<StudentAssignmentReportItem>> ReadStudentAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(StudentAssignments);
        public Task<ReportPage<StudentExamReportItem>> ReadStudentExamsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(Page<StudentExamReportItem>());
        public Task<ReportPage<StudentReleasedGradeReportItem>> ReadStudentGradesAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(StudentGrades);
        public Task<AttendanceReportPage<ParentAttendanceReportItem>> ReadParentAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(AttendancePage<ParentAttendanceReportItem>());
        public Task<ReportPage<ParentAssignmentReportItem>> ReadParentAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(Page<ParentAssignmentReportItem>());
        public Task<ReportPage<ParentExamReportItem>> ReadParentExamsAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(Page<ParentExamReportItem>());
        public Task<bool> HasActiveTeacherAssignmentAsync(AccessContext context, Guid classId, CancellationToken cancellationToken) => Task.FromResult(HasTeacherAssignment);
        public Task<AttendanceReportPage<TeacherAttendanceReportItem>> ReadTeacherAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(AttendancePage<TeacherAttendanceReportItem>());
        public Task<ReportPage<TeacherAssignmentReportItem>> ReadTeacherAssignmentsAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(Page<TeacherAssignmentReportItem>());
        public Task<ReportPage<TeacherExamReportItem>> ReadTeacherExamsAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(Page<TeacherExamReportItem>());
        public Task<TeacherLearnerOverviewDto?> ReadTeacherLearnerAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, Guid learnerId, CancellationToken cancellationToken) => Task.FromResult(TeacherLearner);
        public Task<ReportPage<ManagerEnrollmentReportItem>> ReadManagerEnrollmentsAsync(AccessContext context, DateTime dataAsOfUtc, ManagerEnrollmentReportQuery query, ReportQuerySpec normalizedQuery, CancellationToken cancellationToken) => Task.FromResult(Page<ManagerEnrollmentReportItem>());
        public Task<ReportPage<ManagerAttendanceReportItem>> ReadManagerAttendanceAsync(AccessContext context, DateTime dataAsOfUtc, ReportQuerySpec query, CancellationToken cancellationToken) => Task.FromResult(Page<ManagerAttendanceReportItem>());
        public Task<ReportPage<ManagerAssessmentReportItem>> ReadManagerAssessmentsAsync(AccessContext context, DateTime dataAsOfUtc, ManagerAssessmentReportQuery query, ReportQuerySpec normalizedQuery, CancellationToken cancellationToken) => Task.FromResult(Page<ManagerAssessmentReportItem>());
        public Task<ManagerClassPerformanceDto?> ReadManagerClassAsync(AccessContext context, DateTime dataAsOfUtc, Guid classId, CancellationToken cancellationToken) => Task.FromResult(ManagerClass);
        public Task<ReportPage<ManagerTeacherWorkloadReportItem>> ReadManagerTeacherWorkloadAsync(AccessContext context, DateTime dataAsOfUtc, TeacherWorkloadReportQuery query, ReportQuerySpec normalizedQuery, CancellationToken cancellationToken) => Task.FromResult(Page<ManagerTeacherWorkloadReportItem>());
    }

    private sealed class FakeDashboards : IDashboardReadService
    {
        public Task<StudentDashboardDto> ReadStudentAsync(AccessContext accessContext, DateTime dataAsOfUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ParentDashboardDto> ReadParentAsync(AccessContext accessContext, DateTime dataAsOfUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TeacherDashboardDto> ReadTeacherAsync(AccessContext accessContext, DateTime dataAsOfUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ManagerDashboardDto> ReadManagerAsync(AccessContext accessContext, DateTime dataAsOfUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new ManagerDashboardDto(
                dataAsOfUtc,
                accessContext.OrganizationId!.Value,
                DashboardAudience.Manager,
                new ManagerOperationalCounts(0, 0, 0),
                new ManagerOnboardingChecklist(false, false, false, false, false, false),
                [], [], [], 0,
                new AssignmentGradingQueueSummary(0, 0, 0),
                new ExamGradingQueueSummary(0, 0, 0, 0)));
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
