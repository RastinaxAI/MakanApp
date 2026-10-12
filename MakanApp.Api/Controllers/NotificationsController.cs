using MakanApp.Api.Authentication;
using MakanApp.Application.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NotificationPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationPage>> GetPage(
        [FromQuery] NotificationQuery query,
        CancellationToken cancellationToken) =>
        Ok(await notificationService.GetPageAsync(UserId(), query, cancellationToken));

    [HttpGet("unread-count")]
    [ProducesResponseType<NotificationUnreadCount>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationUnreadCount>> GetUnreadCount(
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken) =>
        Ok(await notificationService.GetUnreadCountAsync(UserId(), organizationId, cancellationToken));

    [HttpPost("{notificationId:guid}/read")]
    [ProducesResponseType<NotificationItem>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationItem>> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken) =>
        Ok(await notificationService.MarkReadAsync(UserId(), notificationId, cancellationToken));

    private Guid UserId() => AuthenticatedSession.GetUserId(User);
}
