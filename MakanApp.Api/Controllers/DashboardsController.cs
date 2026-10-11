using MakanApp.Api.Authentication;
using MakanApp.Application.Reports.Dashboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboards")]
public sealed class DashboardsController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("student")]
    [ProducesResponseType<StudentDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentDashboardDto>> GetStudent(
        CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetStudentAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [HttpGet("parent")]
    [ProducesResponseType<ParentDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ParentDashboardDto>> GetParent(
        CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetParentAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [HttpGet("teacher")]
    [ProducesResponseType<TeacherDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherDashboardDto>> GetTeacher(
        CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetTeacherAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [HttpGet("manager")]
    [ProducesResponseType<ManagerDashboardDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagerDashboardDto>> GetManager(
        CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetManagerAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));
}
