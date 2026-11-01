using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[RequestSizeLimit(4096)]
[Route("api/v1/conversations/{conversationId:guid}/photo")]
public sealed class ConversationPhotosController(ConversationPhotoService photos) : ControllerBase
{
    /// <summary>اتصال عکس Ready از Storage؛ اختیار مدیریت گفتگو و نسخهٔ جاری لازم است.</summary>
    [HttpPut]
    [ProducesResponseType<ConversationPhotoMutationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<ConversationPhotoMutationResult>> Set(Guid conversationId,
        SetConversationPhotoCommand command, CancellationToken cancellationToken) =>
        Ok(await photos.SetAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            conversationId, command.FileAssetId, command.ExpectedVersion, cancellationToken));

    /// <summary>برداشتن Binding عکس بدون حذف Blob یا تاریخچهٔ گفتگو.</summary>
    [HttpDelete]
    [ProducesResponseType<ConversationPhotoMutationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<ConversationPhotoMutationResult>> Remove(Guid conversationId,
        [FromQuery] string expectedVersion, CancellationToken cancellationToken) =>
        Ok(await photos.SetAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            conversationId, null, expectedVersion, cancellationToken));
}
