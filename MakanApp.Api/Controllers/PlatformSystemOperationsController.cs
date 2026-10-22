using MakanApp.Api.Hosting;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/operations")]
public sealed class PlatformSystemOperationsController(
    IPlatformCommerceOperationsService service,
    DataProtectionKeyRingProbe keyRingProbe) : ControllerBase
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
        CancellationToken cancellationToken)
    {
        var health = await service.GetSystemHealthAsync(cancellationToken);
        var ringStatus = keyRingProbe.IsConfigured
            ? await keyRingProbe.IsReadyAsync(cancellationToken) ? "Available" : "Unavailable"
            : keyRingProbe.PersistenceRequired ? "Unavailable" : "DevelopmentOnly";
        return Ok(health with
        {
            Dependencies = health.Dependencies.Append(new PlatformDependencyHealth(
                "DataProtection", ringStatus, "دسترسی جاری به حلقهٔ همان provider میزبان؛ بدون نمایش مسیر یا کلید.")).ToArray()
        });
    }
}
