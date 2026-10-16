using MakanApp.Api.Authentication;
using MakanApp.Application.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications/devices")]
public sealed class NotificationDevicesController(IPushDeviceService deviceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PushDevicePageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PushDevicePageResult>> GetPage(CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, [FromQuery] bool includeRevoked = false) =>
        Ok(await deviceService.GetPageAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            new PushDeviceListQuery(page, pageSize, includeRevoked), cancellationToken));

    [HttpPost("web-push")]
    [EnableRateLimiting("notification-device")]
    [ProducesResponseType<PushDeviceRegistrationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PushDeviceRegistrationResult>> RegisterWebPush(RegisterWebPushCommand command,
        CancellationToken cancellationToken) => Ok(await deviceService.RegisterWebPushAsync(
            AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), command, cancellationToken));

    [HttpPost]
    [EnableRateLimiting("notification-device")]
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
    [EnableRateLimiting("notification-device")]
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
