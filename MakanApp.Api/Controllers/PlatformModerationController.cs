using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/moderation/cases")]
public sealed class PlatformModerationController(
    IPlatformEducationSafetyService service) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PlatformPermissionKeys.ModerationReview)]
    public async Task<ActionResult<PlatformModerationCasePage>> GetCases(
        [FromQuery] ModerationCaseStatus? status,
        [FromQuery] ModerationPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetModerationCasesAsync(
            new PlatformModerationCaseQuery(status, priority, page, pageSize),
            cancellationToken));

    [HttpGet("{caseId:guid}")]
    [Authorize(Policy = PlatformPermissionKeys.ModerationReview)]
    public async Task<ActionResult<PlatformModerationCaseDetail>> GetCase(
        Guid caseId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetModerationCaseAsync(caseId, cancellationToken));

    [HttpPost("{caseId:guid}/start-review")]
    [Authorize(Policy = PlatformPermissionKeys.ModerationReview)]
    public async Task<ActionResult<PlatformModerationCaseDetail>> StartReview(
        Guid caseId,
        PlatformModerationVersionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await service.StartModerationReviewAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            caseId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpPost("{caseId:guid}/resolve")]
    [Authorize(Policy = PlatformPermissionKeys.ModerationReview)]
    public async Task<ActionResult<PlatformModerationCaseDetail>> Resolve(
        Guid caseId,
        PlatformCompleteModerationCommand command,
        CancellationToken cancellationToken) =>
        Ok(await service.ResolveModerationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            caseId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpPost("{caseId:guid}/dismiss")]
    [Authorize(Policy = PlatformPermissionKeys.ModerationReview)]
    public async Task<ActionResult<PlatformModerationCaseDetail>> Dismiss(
        Guid caseId,
        PlatformCompleteModerationCommand command,
        CancellationToken cancellationToken) =>
        Ok(await service.DismissModerationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            caseId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpPost("{caseId:guid}/enforce-content-removal")]
    [Authorize(Policy = PlatformPermissionKeys.ModerationReview)]
    [Authorize(Policy = PlatformPermissionKeys.ModerationEnforce)]
    public async Task<ActionResult<PlatformModerationCaseDetail>> EnforceContentRemoval(
        Guid caseId,
        PlatformModerationVersionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await service.EnforceModerationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            caseId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpGet("{caseId:guid}/evidence/files/{fileAssetId:guid}")]
    [Authorize(Policy = PlatformPermissionKeys.ModerationReview)]
    public async Task<IActionResult> DownloadEvidence(
        Guid caseId,
        Guid fileAssetId,
        CancellationToken cancellationToken)
    {
        var result = await service.DownloadModerationEvidenceAsync(
            caseId,
            fileAssetId,
            cancellationToken);
        return File(result.Content, result.ContentType, result.DownloadFileName, enableRangeProcessing: true);
    }
}
