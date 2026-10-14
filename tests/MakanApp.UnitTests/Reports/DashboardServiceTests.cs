using System.Text.Json;
using MakanApp.Application.Assessment;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Reports;

public sealed class DashboardServiceTests
{
    private static readonly DateTime DataAsOfUtc =
        new(2026, 10, 11, 9, 30, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Manager)]
    public async Task MatchingActiveRoleDelegatesWithSingleServerTimestamp(
        OrganizationRole role)
    {
        var context = CreateContext(role);
        var reader = new RecordingDashboardReadService();
        var service = new DashboardService(
            new StubAccessContextResolver(context),
            reader,
            new FixedTimeProvider(DataAsOfUtc));

        switch (role)
        {
            case OrganizationRole.Student:
                _ = await service.GetStudentAsync(
                    context.UserId,
                    context.SessionId,
                    CancellationToken.None);
                break;
            case OrganizationRole.Parent:
                _ = await service.GetParentAsync(
                    context.UserId,
                    context.SessionId,
                    CancellationToken.None);
                break;
            case OrganizationRole.Teacher:
                _ = await service.GetTeacherAsync(
                    context.UserId,
                    context.SessionId,
                    CancellationToken.None);
                break;
            case OrganizationRole.Manager:
                _ = await service.GetManagerAsync(
                    context.UserId,
                    context.SessionId,
                    CancellationToken.None);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(role));
        }

        Assert.Equal(context, reader.LastContext);
        Assert.Equal(DataAsOfUtc, reader.LastDataAsOfUtc);
    }

    [Fact]
    public async Task ClientCannotUseStudentEndpointFromTeacherWorkspace()
    {
        var context = CreateContext(OrganizationRole.Teacher);
        var service = new DashboardService(
            new StubAccessContextResolver(context),
            new RecordingDashboardReadService(),
            new FixedTimeProvider(DataAsOfUtc));

        var exception = await Assert.ThrowsAsync<OrganizationException>(() =>
            service.GetStudentAsync(context.UserId, context.SessionId, CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.WorkspaceNotAllowed, exception.Code);
    }

    [Fact]
    public async Task PersonalWorkspaceCannotReadOrganizationDashboard()
    {
        var context = new AccessContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceType.Personal,
            null,
            null,
            null,
            null);
        var service = new DashboardService(
            new StubAccessContextResolver(context),
            new RecordingDashboardReadService(),
            new FixedTimeProvider(DataAsOfUtc));

        var exception = await Assert.ThrowsAsync<OrganizationException>(() =>
            service.GetManagerAsync(context.UserId, context.SessionId, CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.WorkspaceNotAllowed, exception.Code);
    }

    [Fact]
    public void PublicDashboardContractsDoNotSerializeSensitiveInternalFields()
    {
        var json = JsonSerializer.Serialize(new object[]
        {
            RecordingDashboardReadService.Student,
            RecordingDashboardReadService.Parent,
            RecordingDashboardReadService.Teacher,
            RecordingDashboardReadService.Manager
        });

        string[] forbiddenNames =
        [
            "CodeHash",
            "Salt",
            "AnswerKey",
            "CorrectOption",
            "TeacherPrivateNote",
            "EvaluatorPrivateNote",
            "MessageBody",
            "Copilot",
            "RowVersion",
            "SessionToken"
        ];

        Assert.All(
            forbiddenNames,
            forbiddenName => Assert.DoesNotContain(
                forbiddenName,
                json,
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PopulatedDashboardContractsSerializeOnlyAudienceSafeFields()
    {
        var id = Guid.NewGuid();
        var session = new DashboardSessionItem(id, id, "Class", "Session", DataAsOfUtc,
            DataAsOfUtc.AddHours(1), "Asia/Tehran", null, SessionStatus.Scheduled);
        var assignment = new DashboardAssignmentItem(id, id, id, "Class", "Assignment",
            DataAsOfUtc.AddHours(2), true, StudentAssignmentActionStatus.NotStarted);
        var exam = new DashboardExamItem(id, id, id, "Class", "Exam", DataAsOfUtc,
            DataAsOfUtc.AddHours(3), 30, ExamAttemptStatus.Finalized,
            ExamResultReleaseStatus.Released, 18m, 20m, DataAsOfUtc);
        var student = RecordingDashboardReadService.Student with
        {
            NextSession = session,
            NearestAssignmentDeadline = assignment,
            RecentReleasedFeedback = [new(id, id, "Assignment", 17.25m, 20m, "Learner feedback", DataAsOfUtc)],
            UpcomingExams = [exam]
        };
        var parent = RecordingDashboardReadService.Parent with
        {
            SelectedLearner = new(id, "Child"),
            UpcomingSessions = [session],
            Assignments = [assignment],
            ReleasedAssignmentResults = [new(id, id, "Assignment", 17.25m, 20m, "Guardian feedback", DataAsOfUtc)],
            Exams = [exam]
        };
        var teacher = RecordingDashboardReadService.Teacher with
        {
            AssignedClasses = [new(id, "Class", "Course", "Period", 1)],
            UpcomingSessions = [session],
            AttendancePending = [new(id, id, "Class", "Session", DataAsOfUtc, DataAsOfUtc.AddHours(1), 1, 0, 1)]
        };
        var manager = RecordingDashboardReadService.Manager with
        {
            UpcomingSessions = [session],
            SessionsMissingMeetingLink = [session],
            AttendancePending = teacher.AttendancePending
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var forbidden = new[] { "answerKey", "correctOptionId", "isCorrect", "prompt", "options",
            "learnerFeedback", "teacherPrivateNote", "evaluatorPrivateNote", "messageBody",
            "copilotHistory", "sessionToken", "accessToken", "refreshToken", "codeHash", "rowVersion" };

        foreach (var dashboard in new object[] { student, parent, teacher, manager })
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(dashboard, options));
            Assert.True(document.RootElement.TryGetProperty("dataAsOfUtc", out _));
            var forbiddenForAudience = dashboard is StudentDashboardDto
                ? forbidden.Where(name => name != "learnerFeedback")
                : forbidden;
            AssertSafeProperties(document.RootElement, forbiddenForAudience.ToHashSet(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static void AssertSafeProperties(JsonElement element, HashSet<string> forbidden)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                Assert.DoesNotContain(property.Name, forbidden);
                AssertSafeProperties(property.Value, forbidden);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) AssertSafeProperties(item, forbidden);
        }
    }

    private static AccessContext CreateContext(OrganizationRole role) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            WorkspaceType.Organization,
            Guid.NewGuid(),
            Guid.NewGuid(),
            role,
            role == OrganizationRole.Parent ? Guid.NewGuid() : null);

    private sealed class StubAccessContextResolver(AccessContext context) : IAccessContextResolver
    {
        public Task<AccessContext> ResolveAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(context);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class RecordingDashboardReadService : IDashboardReadService
    {
        private static readonly Guid OrganizationId = Guid.NewGuid();

        private static DashboardAttendanceSummary EmptyAttendance { get; } =
            new(0, 0, 0, 0, 0, 0, 0);

        private static AssignmentGradingQueueSummary EmptyAssignmentQueue { get; } =
            new(0, 0, 0);

        private static ExamGradingQueueSummary EmptyExamQueue { get; } =
            new(0, 0, 0, 0);

        public static StudentDashboardDto Student { get; } = new(
            DataAsOfUtc,
            OrganizationId,
            DashboardAudience.Student,
            null,
            null,
            [],
            [],
            EmptyAttendance,
            0);

        public static ParentDashboardDto Parent { get; } = new(
            DataAsOfUtc,
            OrganizationId,
            DashboardAudience.Parent,
            null,
            [],
            [],
            [],
            [],
            EmptyAttendance,
            0);

        public static TeacherDashboardDto Teacher { get; } = new(
            DataAsOfUtc,
            OrganizationId,
            DashboardAudience.Teacher,
            [],
            [],
            [],
            EmptyAssignmentQueue,
            EmptyExamQueue);

        public static ManagerDashboardDto Manager { get; } = new(
            DataAsOfUtc,
            OrganizationId,
            DashboardAudience.Manager,
            new ManagerOperationalCounts(0, 0, 0),
            new ManagerOnboardingChecklist(true, false, false, false, false, false),
            [],
            [],
            [],
            0,
            EmptyAssignmentQueue,
            EmptyExamQueue);

        public AccessContext? LastContext { get; private set; }
        public DateTime? LastDataAsOfUtc { get; private set; }

        public Task<StudentDashboardDto> ReadStudentAsync(
            AccessContext accessContext,
            DateTime dataAsOfUtc,
            CancellationToken cancellationToken)
        {
            Record(accessContext, dataAsOfUtc);
            return Task.FromResult(Student with
            {
                OrganizationId = accessContext.OrganizationId!.Value,
                DataAsOfUtc = dataAsOfUtc
            });
        }

        public Task<ParentDashboardDto> ReadParentAsync(
            AccessContext accessContext,
            DateTime dataAsOfUtc,
            CancellationToken cancellationToken)
        {
            Record(accessContext, dataAsOfUtc);
            return Task.FromResult(Parent with
            {
                OrganizationId = accessContext.OrganizationId!.Value,
                DataAsOfUtc = dataAsOfUtc
            });
        }

        public Task<TeacherDashboardDto> ReadTeacherAsync(
            AccessContext accessContext,
            DateTime dataAsOfUtc,
            CancellationToken cancellationToken)
        {
            Record(accessContext, dataAsOfUtc);
            return Task.FromResult(Teacher with
            {
                OrganizationId = accessContext.OrganizationId!.Value,
                DataAsOfUtc = dataAsOfUtc
            });
        }

        public Task<ManagerDashboardDto> ReadManagerAsync(
            AccessContext accessContext,
            DateTime dataAsOfUtc,
            CancellationToken cancellationToken)
        {
            Record(accessContext, dataAsOfUtc);
            return Task.FromResult(Manager with
            {
                OrganizationId = accessContext.OrganizationId!.Value,
                DataAsOfUtc = dataAsOfUtc
            });
        }

        private void Record(AccessContext context, DateTime dataAsOfUtc)
        {
            LastContext = context;
            LastDataAsOfUtc = dataAsOfUtc;
        }
    }
}
