using MakanApp.Api.Authentication;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notification-preferences")]
public sealed class NotificationPreferencesController(
    INotificationPreferenceService preferenceService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<NotificationPreferenceItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<NotificationPreferenceItem>>> Get(
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken) =>
        Ok(await preferenceService.GetAsync(UserId(), organizationId, cancellationToken));

    [HttpPut("{category}")]
    [ProducesResponseType<NotificationPreferenceItem>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPreferenceItem>> Update(
        NotificationPreferenceCategory category,
        [FromQuery] Guid? organizationId,
        UpdateNotificationPreferenceCommand command,
        CancellationToken cancellationToken) =>
        Ok(await preferenceService.UpdateAsync(
            UserId(), organizationId, category, command, cancellationToken));

    private Guid UserId() => AuthenticatedSession.GetUserId(User);
}
