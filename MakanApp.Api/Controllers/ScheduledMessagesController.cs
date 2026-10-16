using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[RequestSizeLimit(64 * 1024)]
[Route("api/v1/conversations/{conversationId:guid}/scheduled-messages")]
[EnableRateLimiting("messaging-send")]
public sealed class ScheduledMessagesController(ScheduledMessageService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ScheduledMessageResult>(StatusCodes.Status200OK)]
    public Task<ScheduledMessageResult> Schedule(Guid conversationId, ScheduleMessageCommand command, CancellationToken ct) =>
        service.ScheduleAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, command, HttpContext.TraceIdentifier, ct);
    [HttpGet]
    [ProducesResponseType<ScheduledMessagePageResult>(StatusCodes.Status200OK)]
    public Task<ScheduledMessagePageResult> Get(Guid conversationId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        service.GetAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, page, pageSize, ct);
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<ScheduledMessageResult>(StatusCodes.Status200OK)]
    public Task<ScheduledMessageResult> Cancel(Guid conversationId, Guid id, CancelScheduledMessageCommand command, CancellationToken ct) =>
        service.CancelAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, id, command, HttpContext.TraceIdentifier, ct);
}
