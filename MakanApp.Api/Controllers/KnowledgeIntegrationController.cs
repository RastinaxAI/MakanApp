using MakanApp.Api.Authentication;
using MakanApp.Application.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("ai-chat")]
[Route("api/v1")]
public sealed class KnowledgeIntegrationController(KnowledgeIntegrationService service) : ControllerBase
{
    private Guid Actor => AuthenticatedSession.GetUserId(User);
    private Guid Session => AuthenticatedSession.GetSessionId(User);

    [HttpPost("sources/{sourceId:guid}/versions/{version:int}/knowledge/ingest")]
    [ProducesResponseType<KnowledgeProcessingStatus>(202)]
    [ProducesResponseType<KnowledgeProcessingStatus>(200)]
    public async Task<ActionResult<KnowledgeProcessingStatus>> Ingest(Guid sourceId, int version, KnowledgeMutationCommand command, CancellationToken ct)
    {
        var result = await service.IngestAsync(Actor, Session, new(sourceId, version), HttpContext.TraceIdentifier, command, ct);
        return StatusCode(result.StatusCode, result.Status);
    }

    [HttpGet("sources/{sourceId:guid}/versions/{version:int}/knowledge/status")]
    public async Task<ActionResult<KnowledgeProcessingStatus>> Status(Guid sourceId, int version, [FromQuery] bool wait, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await service.StatusAsync(Actor, Session, new(sourceId, version), wait, HttpContext.TraceIdentifier, ct));
    }

    [HttpPost("sources/{sourceId:guid}/versions/{version:int}/knowledge/delete")]
    public async Task<ActionResult<KnowledgeProcessingStatus>> Delete(Guid sourceId, int version, KnowledgeMutationCommand command, CancellationToken ct) =>
        Ok(await service.CommandAsync(Actor, Session, new(sourceId, version), "delete", HttpContext.TraceIdentifier, command, ct));

    [HttpPost("sources/{sourceId:guid}/versions/{version:int}/knowledge/supersede")]
    public async Task<ActionResult<KnowledgeProcessingStatus>> Supersede(Guid sourceId, int version, KnowledgeMutationCommand command, CancellationToken ct) =>
        Ok(await service.CommandAsync(Actor, Session, new(sourceId, version), "supersede", HttpContext.TraceIdentifier, command, ct));

    [HttpPost("sources/{sourceId:guid}/versions/{version:int}/knowledge/reindex")]
    [ProducesResponseType<KnowledgeProcessingStatus>(202)]
    public async Task<ActionResult<KnowledgeProcessingStatus>> Reindex(Guid sourceId, int version, KnowledgeMutationCommand command, CancellationToken ct) =>
        StatusCode(202, await service.CommandAsync(Actor, Session, new(sourceId, version), "reindex", HttpContext.TraceIdentifier, command, ct));

    [HttpPost("ai/knowledge/retrieve")]
    public async Task<ActionResult<KnowledgeRetrieveResponse>> Retrieve(KnowledgeQueryCommand command, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await service.RetrieveAsync(Actor, Session, HttpContext.TraceIdentifier, command, ct));
    }

    [HttpPost("ai/knowledge/rag/answer")]
    public async Task<ActionResult<KnowledgeRagResponse>> Answer(KnowledgeQueryCommand command, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await service.AnswerAsync(Actor, Session, HttpContext.TraceIdentifier, command, ct));
    }
}
