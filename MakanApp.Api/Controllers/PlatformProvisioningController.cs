using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/provisioning")]
public sealed class PlatformProvisioningController(
    IPlatformOperationsService operationsService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PlatformPermissionKeys.ProvisioningRead)]
    public async Task<ActionResult<PlatformProvisioningPage>> GetRequests(
        [FromQuery] PlatformProvisioningStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string? clientOperationId = null,
        CancellationToken cancellationToken = default) =>
        Ok(await operationsService.GetProvisioningAsync(
            new PlatformProvisioningQuery(status, page, pageSize,
                Request.Query.ContainsKey(nameof(clientOperationId)) ? clientOperationId ?? string.Empty : null),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = PlatformPermissionKeys.ProvisioningManage)]
    public async Task<ActionResult<PlatformProvisioningResult>> Provision(
        ProvisionOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await operationsService.ProvisionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Replayed ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }
}
