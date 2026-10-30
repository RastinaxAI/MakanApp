using MakanApp.Api.Authentication;
using MakanApp.Application.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/sources")]
public sealed class EducationalSourcesController(EducationalSourceService service) : ControllerBase
{
    private Guid Actor => AuthenticatedSession.GetUserId(User);
    private Guid Session => AuthenticatedSession.GetSessionId(User);
    [HttpGet]
    public async Task<ActionResult<SourcePage>> List([FromQuery] SourceCatalogQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(Actor, Session, query, false, ct));
    [HttpPost]
    public async Task<ActionResult<EducationalSourceResult>> Create(CreateEducationalSourceCommand command, CancellationToken ct)
    {
        var result = await service.CreateAsync(Actor, Session, command, ct);
        return StatusCode(result.Replayed ? 200 : 201, result);
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EducationalSourceResult>> Get(Guid id, [FromQuery] int? revision, CancellationToken ct) =>
        Ok(await service.GetAsync(Actor, Session, id, revision, ct));
    [HttpGet("{id:guid}/processing")]
    public async Task<ActionResult<EducationalSourceResult>> Processing(Guid id, [FromQuery] int? revision, CancellationToken ct) =>
        Ok(await service.GetAsync(Actor, Session, id, revision, ct));
    [HttpPatch("{id:guid}/draft")]
    public async Task<ActionResult<EducationalSourceResult>> Update(Guid id, UpdateSourceDraftCommand command, CancellationToken ct) =>
        Ok(await service.MutateAsync(Actor, Session, id, command, null, null, null, null, ct));
    [HttpPost("{id:guid}/versions")]
    public async Task<ActionResult<EducationalSourceResult>> Version(Guid id, CreateSourceVersionCommand command, CancellationToken ct) =>
        StatusCode(201, await service.MutateAsync(Actor, Session, id, null, command, null, null, null, ct));
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<EducationalSourceResult>> Publish(Guid id, SourceMutationCommand command, CancellationToken ct) =>
        Ok(await service.MutateAsync(Actor, Session, id, null, null, command, null, null, ct));
    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<EducationalSourceResult>> Archive(Guid id, SourceMutationCommand command, CancellationToken ct) =>
        Ok(await service.MutateAsync(Actor, Session, id, null, null, null, command, null, ct));
    [HttpPut("{id:guid}/visibility")]
    public async Task<ActionResult<EducationalSourceResult>> Visibility(Guid id, SourceVisibilityCommand command, CancellationToken ct) =>
        Ok(await service.MutateAsync(Actor, Session, id, null, null, null, null, command, ct));
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, [FromQuery] int? revision, CancellationToken ct)
    {
        var result = await service.DownloadAsync(Actor, Session, id, revision, ct);
        Response.ContentLength = result.SizeBytes; Response.Headers.CacheControl = "private, no-store"; Response.Headers.Vary = "Authorization";
        return File(result.Content, result.ContentType, result.DownloadFileName, enableRangeProcessing: true);
    }
}
