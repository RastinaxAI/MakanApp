using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/conversations/{conversationId:guid}/moderation/cases")]
public sealed class ConversationModerationController(IModerationService moderationService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ModerationCasePageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationCasePageResult>> GetCases(
        Guid conversationId,
        [FromQuery] ModerationCaseStatus? status,
        [FromQuery] ModerationPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await moderationService.GetCasesAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            new GetModerationCasesQuery(status, priority, page, pageSize),
            cancellationToken));

    [HttpGet("{caseId:guid}")]
    [ProducesResponseType<ModerationCaseDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationCaseDetailResult>> GetCase(
        Guid conversationId,
        Guid caseId,
        CancellationToken cancellationToken) =>
        Ok(await moderationService.GetCaseAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            caseId,
            cancellationToken));

    [HttpPost("{caseId:guid}/start-review")]
    [ProducesResponseType<ModerationCaseDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationCaseDetailResult>> StartReview(
        Guid conversationId,
        Guid caseId,
        StartModerationReviewCommand command,
        CancellationToken cancellationToken) =>
        Ok(await moderationService.StartReviewAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            caseId,
            command,
            cancellationToken));

    [HttpPost("{caseId:guid}/resolve")]
    [ProducesResponseType<ModerationCaseDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationCaseDetailResult>> Resolve(
        Guid conversationId,
        Guid caseId,
        CompleteModerationCaseCommand command,
        CancellationToken cancellationToken) =>
        Ok(await moderationService.ResolveAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            caseId,
            command,
            cancellationToken));

    [HttpPost("{caseId:guid}/dismiss")]
    [ProducesResponseType<ModerationCaseDetailResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationCaseDetailResult>> Dismiss(
        Guid conversationId,
        Guid caseId,
        CompleteModerationCaseCommand command,
        CancellationToken cancellationToken) =>
        Ok(await moderationService.DismissAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            caseId,
            command,
            cancellationToken));

    [HttpGet("{caseId:guid}/evidence/files/{fileAssetId:guid}")]
    public async Task<IActionResult> DownloadEvidence(
        Guid conversationId,
        Guid caseId,
        Guid fileAssetId,
        CancellationToken cancellationToken)
    {
        var result = await moderationService.DownloadEvidenceAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            caseId,
            fileAssetId,
            cancellationToken);
        return File(result.Content, result.ContentType, result.DownloadFileName, enableRangeProcessing: true);
    }
}
