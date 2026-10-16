using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[RequestSizeLimit(64 * 1024)]
[Authorize]
[Route("api/v1/messaging/folders")]
[EnableRateLimiting("messaging-send")]
public sealed class MessagingFoldersController(MessagingPresentationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ChatFolderResult>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyCollection<ChatFolderResult>> Get(CancellationToken cancellationToken) => service.GetFoldersAsync(
        AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), cancellationToken);

    [HttpPut("{folderId:guid}")]
    [ProducesResponseType<ChatFolderResult>(StatusCodes.Status200OK)]
    public Task<ChatFolderResult> Save(Guid folderId, SaveChatFolderCommand command, CancellationToken cancellationToken) => service.SaveFolderAsync(
        AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), folderId, command, cancellationToken);

    [HttpDelete("{folderId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid folderId, [FromQuery] string expectedVersion, CancellationToken cancellationToken)
    {
        await service.DeleteFolderAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), folderId, expectedVersion, cancellationToken);
        return NoContent();
    }
}
