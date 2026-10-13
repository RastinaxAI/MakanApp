using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/operations")]
public sealed class PlatformSystemOperationsController(
    IPlatformCommerceOperationsService service) : ControllerBase
{
    [HttpGet("notifications")]
    [Authorize(Policy = PlatformPermissionKeys.NotificationsRead)]
    public async Task<ActionResult<PlatformNotificationOperations>> GetNotifications(
        CancellationToken cancellationToken) =>
        Ok(await service.GetNotificationOperationsAsync(cancellationToken));

    [HttpGet("jobs")]
    [Authorize(Policy = PlatformPermissionKeys.OperationsRead)]
    public async Task<ActionResult<PlatformJobOperations>> GetJobs(
        CancellationToken cancellationToken) =>
        Ok(await service.GetJobOperationsAsync(cancellationToken));

    [HttpGet("health")]
    [Authorize(Policy = PlatformPermissionKeys.OperationsRead)]
    public async Task<ActionResult<PlatformSystemHealth>> GetHealth(
        CancellationToken cancellationToken) =>
        Ok(await service.GetSystemHealthAsync(cancellationToken));
}
