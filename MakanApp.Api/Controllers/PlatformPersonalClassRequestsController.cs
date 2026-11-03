using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/admin/v1/personal-class-requests")]
public sealed class PlatformPersonalClassRequestsController(PersonalClassFormationService service) : ControllerBase
{
    private PersonalClassActor Actor => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpGet, Authorize(Policy = PlatformPermissionKeys.PersonalClassesRead)]
    public async Task<ActionResult<PersonalClassPage<PersonalClassRequestDto>>> List([FromQuery] PersonalClassRequestStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) => Ok(await service.ListAsync(Actor, true, status, page, pageSize, ct));
    [HttpGet("{id:guid}"), Authorize(Policy = PlatformPermissionKeys.PersonalClassesRead)]
    public async Task<ActionResult<PersonalClassRequestDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(Actor, id, true, ct));
    [HttpPost("{id:guid}/review"), Authorize(Policy = PlatformPermissionKeys.PersonalClassesReview)]
    public async Task<ActionResult<PersonalClassRequestDto>> Review(Guid id, ReviewPersonalClassRequestCommand command, CancellationToken ct) => Ok(await service.ReviewAsync(Actor, id, command, ct));
    [HttpGet("{id:guid}/history"), Authorize(Policy = PlatformPermissionKeys.PersonalClassesRead)]
    public async Task<ActionResult<PersonalClassPage<PersonalClassDecisionDto>>> History(Guid id, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30, CancellationToken ct = default) => Ok(await service.HistoryAsync(Actor, id, true, page, pageSize, ct));
}
