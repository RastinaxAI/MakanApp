using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/personal-class-requests")]
public sealed class PersonalClassRequestsController(PersonalClassFormationService service) : ControllerBase
{
    private PersonalClassActor Actor => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpPost]
    public async Task<ActionResult<PersonalClassRequestDto>> Create(CreatePersonalClassRequestCommand command, CancellationToken ct) =>
        StatusCode(201, await service.CreateAsync(Actor, command, ct));
    [HttpGet]
    public async Task<ActionResult<PersonalClassPage<PersonalClassRequestDto>>> List([FromQuery] PersonalClassRequestStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) => Ok(await service.ListAsync(Actor, false, status, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PersonalClassRequestDto>> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(Actor, id, false, ct));
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PersonalClassRequestDto>> Edit(Guid id, EditPersonalClassRequestCommand command, CancellationToken ct) => Ok(await service.EditAsync(Actor, id, command, ct));
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<PersonalClassRequestDto>> Submit(Guid id, PersonalClassVersionCommand command, CancellationToken ct) => Ok(await service.TransitionAsync(Actor, id, command, true, ct));
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PersonalClassRequestDto>> Cancel(Guid id, PersonalClassVersionCommand command, CancellationToken ct) => Ok(await service.TransitionAsync(Actor, id, command, false, ct));
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<PersonalClassPage<PersonalClassDecisionDto>>> History(Guid id, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30, CancellationToken ct = default) => Ok(await service.HistoryAsync(Actor, id, false, page, pageSize, ct));
}
