using MakanApp.Application.Organization;

namespace MakanApp.Application.Reports.Dashboards;

public interface IDashboardService
{
    Task<StudentDashboardDto> GetStudentAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ParentDashboardDto> GetParentAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<TeacherDashboardDto> GetTeacherAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ManagerDashboardDto> GetManagerAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface IDashboardReadService
{
    Task<StudentDashboardDto> ReadStudentAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken);

    Task<ParentDashboardDto> ReadParentAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken);

    Task<TeacherDashboardDto> ReadTeacherAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken);

    Task<ManagerDashboardDto> ReadManagerAsync(
        AccessContext accessContext,
        DateTime dataAsOfUtc,
        CancellationToken cancellationToken);
}
