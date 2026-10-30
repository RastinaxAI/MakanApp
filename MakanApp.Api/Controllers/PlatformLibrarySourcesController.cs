using MakanApp.Api.Authentication;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/library/sources")]
public sealed class PlatformLibrarySourcesController(EducationalSourceService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SourcePage>> List([FromQuery] SourceCatalogQuery query, CancellationToken ct) =>
        Ok(await service.ListAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            query with { Scope = EducationalSourceScope.Library, ClassId = null }, true, ct));
    [HttpPost]
    public async Task<ActionResult<EducationalSourceResult>> Create(CreateEducationalSourceCommand command, CancellationToken ct)
    {
        var result = await service.CreateAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            command with { Scope = EducationalSourceScope.Library, ClassId = null }, ct);
        return StatusCode(result.Replayed ? 200 : 201, result);
    }
}
