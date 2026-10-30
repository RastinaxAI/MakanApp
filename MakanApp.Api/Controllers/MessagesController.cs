using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/messages")]
public sealed class MessagesController(IMessagingSafetyService safetyService) : ControllerBase
{
    [HttpGet("search")]
    [EnableRateLimiting("messaging-search")]
    [ProducesResponseType<MessageSearchPageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageSearchPageResult>> Search(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] Guid? conversationId,
        [FromQuery] Guid? senderUserId,
        [FromQuery] MessageKind? kind,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        [FromQuery] string? savedLabel,
        [FromQuery] bool? hasAttachment,
        [FromQuery] bool? hasLinks,
        [FromQuery] Guid? topicId,
        [FromQuery] ConversationScope? scope,
        CancellationToken cancellationToken) =>
        Ok(await safetyService.SearchMessagesAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            new SearchMessagesQuery(
                query,
                conversationId,
                kind,
                fromUtc,
                toUtc,
                cursor,
                limit,
                senderUserId,
                savedLabel,
                hasAttachment,
                hasLinks,
                topicId,
                scope),
            cancellationToken));
}
