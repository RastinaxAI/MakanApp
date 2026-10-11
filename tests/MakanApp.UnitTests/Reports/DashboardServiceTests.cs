using System.Text.Json;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Dashboards;
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
