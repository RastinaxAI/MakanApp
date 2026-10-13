using MakanApp.Api.Authentication;
using MakanApp.Application.Ai;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/ai")]
public sealed class AiController(IAiChatService chatService) : ControllerBase
{
    [HttpPost("chat")]
    [EnableRateLimiting("ai-chat")]
    [ProducesResponseType<AiChatResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AiChatResult>> Chat(
        [FromBody] SendAiChatCommand command,
        CancellationToken cancellationToken) =>
        Ok(await chatService.ChatAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier,
            command,
            cancellationToken));
}
