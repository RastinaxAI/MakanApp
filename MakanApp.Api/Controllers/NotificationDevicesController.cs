using MakanApp.Api.Authentication;
using MakanApp.Application.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications/devices")]
public sealed class NotificationDevicesController(IPushDeviceService deviceService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PushDeviceRegistrationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PushDeviceRegistrationResult>> Register(
        RegisterPushDeviceCommand command,
        CancellationToken cancellationToken) =>
        Ok(await deviceService.RegisterAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken));

    [HttpDelete("{registrationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(
        Guid registrationId,
        CancellationToken cancellationToken)
    {
        await deviceService.RevokeAsync(
            AuthenticatedSession.GetUserId(User), registrationId, cancellationToken);
        return NoContent();
    }
}
