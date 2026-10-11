using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Reports.Dashboards;

public sealed class DashboardService(
    IAccessContextResolver accessContextResolver,
    IDashboardReadService readService,
    TimeProvider timeProvider) : IDashboardService
{
    public async Task<StudentDashboardDto> GetStudentAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Student, cancellationToken);
        return await readService.ReadStudentAsync(context, UtcNow(), cancellationToken);
    }

    public async Task<ParentDashboardDto> GetParentAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Parent, cancellationToken);
        return await readService.ReadParentAsync(context, UtcNow(), cancellationToken);
    }

    public async Task<TeacherDashboardDto> GetTeacherAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Teacher, cancellationToken);
        return await readService.ReadTeacherAsync(context, UtcNow(), cancellationToken);
    }

    public async Task<ManagerDashboardDto> GetManagerAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await ResolveAsync(userId, sessionId, OrganizationRole.Manager, cancellationToken);
        return await readService.ReadManagerAsync(context, UtcNow(), cancellationToken);
    }

    private async Task<AccessContext> ResolveAsync(
        Guid userId,
        Guid sessionId,
        OrganizationRole requiredRole,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != requiredRole)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.WorkspaceNotAllowed,
                "داشبورد در فضای سازمانی و فقط برای نقش فعال متناظر قابل مشاهده است.");
        }

        return context;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
