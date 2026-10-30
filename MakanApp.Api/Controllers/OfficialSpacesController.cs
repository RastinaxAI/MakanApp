using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/organization")]
public sealed class OfficialSpacesController(OfficialSpacesService service) : ControllerBase
{
    [HttpGet("official-spaces")]
    [ProducesResponseType<OfficialSpacesPairResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OfficialSpacesPairResult>> Organization(CancellationToken ct) =>
        Ok(await service.ReadAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), null, ct));

    [HttpGet("classes/{classId:guid}/official-spaces")]
    [ProducesResponseType<OfficialSpacesPairResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OfficialSpacesPairResult>> Class(Guid classId, CancellationToken ct) =>
        Ok(await service.ReadAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), classId, ct));

    [HttpGet("classes/{classId:guid}/official-spaces/activities/{eventId:guid}")]
    [ProducesResponseType<IReadOnlyList<AcademicActivityDeliveryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AcademicActivityDeliveryResult>>> Delivery(Guid classId, Guid eventId, CancellationToken ct) =>
        Ok(await service.ReadDeliveryAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), classId, eventId, ct));

    [HttpGet("classes/{classId:guid}/official-spaces/activities")]
    [ProducesResponseType<IReadOnlyList<AcademicActivityDeliveryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AcademicActivityDeliveryResult>>> SourceDelivery(Guid classId,
        [FromQuery] AcademicActivityKind kind, [FromQuery] Guid sourceVersionId, [FromQuery] int revision, CancellationToken ct) =>
        Ok(await service.ReadSourceDeliveryAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            classId, kind, sourceVersionId, revision, ct));
}
