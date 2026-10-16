using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[RequestSizeLimit(64 * 1024)]
[Authorize]
[Route("api/v1/conversations/saved/messages/{messageId:guid}/labels")]
[EnableRateLimiting("messaging-send")]
public sealed class SavedMessageLabelsController(MessagingPresentationService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SavedMessageLabelsResult>(StatusCodes.Status200OK)]
    public Task<SavedMessageLabelsResult> Get(Guid messageId, CancellationToken cancellationToken) => service.GetSavedLabelsAsync(
        AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), messageId, cancellationToken);

    [HttpPut]
    [ProducesResponseType<SavedMessageLabelsResult>(StatusCodes.Status200OK)]
    public Task<SavedMessageLabelsResult> Replace(Guid messageId, ReplaceSavedLabelsCommand command, CancellationToken cancellationToken) => service.ReplaceSavedLabelsAsync(
        AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), messageId, command, cancellationToken);
}
