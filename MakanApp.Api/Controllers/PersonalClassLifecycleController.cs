using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/personal-classes")]
public sealed class PersonalClassLifecycleController(PersonalClassLifecycleService service) : ControllerBase
{
    private PersonalClassActor Actor => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpGet("page")]
    public async Task<ActionResult<PersonalClassPage<PersonalClassDto>>> Classes([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.ClassesAsync(Actor, page, pageSize, ct));
    [HttpGet("invitations/page")]
    public async Task<ActionResult<PersonalClassPage<PersonalClassInvitationDto>>> Invitations([FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.InvitationsAsync(Actor, page, pageSize, ct));
    [HttpGet("{id:guid}/memberships")]
    public async Task<ActionResult<PersonalClassPage<PersonalClassMembershipDto>>> Members(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.MembersAsync(Actor, id, page, pageSize, ct));
    [HttpPut("{id:guid}/settings")]
    public async Task<ActionResult<PersonalClassDto>> Configure(Guid id, ConfigurePersonalClassCommand command, CancellationToken ct) => Ok(await service.ConfigureAsync(Actor, id, command, ct));
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<PersonalClassDto>> Activate(Guid id, PersonalClassReasonCommand command, CancellationToken ct) => Ok(await service.StateAsync(Actor, id, command, PersonalClassState.Active, ct));
    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<PersonalClassDto>> Suspend(Guid id, PersonalClassReasonCommand command, CancellationToken ct) => Ok(await service.StateAsync(Actor, id, command, PersonalClassState.Suspended, ct));
    [HttpPut("{id:guid}/memberships/{memberId:guid}/roles")]
    public async Task<ActionResult<PersonalClassMembershipDto>> Roles(Guid id, Guid memberId, ChangePersonalClassRolesCommand command, CancellationToken ct) => Ok(await service.RolesAsync(Actor, id, memberId, command, ct));
    [HttpDelete("{id:guid}/memberships/{memberId:guid}")]
    public async Task<IActionResult> End(Guid id, Guid memberId, [FromBody] PersonalClassReasonCommand command, CancellationToken ct)
    { await service.EndAsync(Actor, id, memberId, command, ct); return NoContent(); }
    [HttpPost("invitations/{id:guid}/decline")]
    public async Task<ActionResult<PersonalClassInvitationDto>> Decline(Guid id, PersonalClassReasonCommand command, CancellationToken ct) => Ok(await service.CloseInvitationAsync(Actor, id, command, true, ct));
    [HttpPost("invitations/{id:guid}/revoke")]
    public async Task<ActionResult<PersonalClassInvitationDto>> Revoke(Guid id, PersonalClassReasonCommand command, CancellationToken ct) => Ok(await service.CloseInvitationAsync(Actor, id, command, false, ct));
    [HttpPost("{id:guid}/join-requests")]
    public async Task<ActionResult<PersonalClassJoinDto>> RequestJoin(Guid id, CreatePersonalClassJoinCommand command, CancellationToken ct) => StatusCode(201, await service.RequestJoinAsync(Actor, id, command, ct));
    [HttpPost("join-requests/{id:guid}/decision")]
    public async Task<ActionResult<PersonalClassJoinDto>> DecideJoin(Guid id, DecidePersonalClassJoinCommand command, CancellationToken ct) => Ok(await service.DecideJoinAsync(Actor, id, command, ct));
    [HttpGet("join-requests")]
    public async Task<ActionResult<PersonalClassPage<PersonalClassJoinDto>>> Joins([FromQuery] Guid? classId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.JoinsAsync(Actor, classId, page, pageSize, ct));
    [HttpPost("{id:guid}/manager-transfers")]
    public async Task<ActionResult<PersonalClassTransferDto>> Transfer(Guid id, CreatePersonalClassTransferCommand command, CancellationToken ct) => StatusCode(201, await service.TransferAsync(Actor, id, command, ct));
    [HttpPost("manager-transfers/{id:guid}/decision")]
    public async Task<ActionResult<PersonalClassTransferDto>> DecideTransfer(Guid id, DecidePersonalClassTransferCommand command, CancellationToken ct) => Ok(await service.DecideTransferAsync(Actor, id, command, ct));
    [HttpGet("manager-transfers")]
    public async Task<ActionResult<PersonalClassPage<PersonalClassTransferDto>>> Transfers([FromQuery] Guid? classId, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.TransfersAsync(Actor, classId, page, pageSize, ct));
    [HttpGet("{id:guid}/spaces")]
    public async Task<ActionResult<OfficialSpacesPairResult>> Spaces(Guid id, CancellationToken ct) => Ok(await service.SpacesAsync(Actor, id, ct));
    [HttpPost("{id:guid}/spaces/repair")]
    public async Task<IActionResult> Repair(Guid id, RepairPersonalClassSpacesCommand command, CancellationToken ct)
    { await service.RepairAsync(Actor, id, command, ct); return Accepted(); }
    [HttpGet("{id:guid}/spaces/backfill-plan")]
    public async Task<ActionResult<PersonalClassBackfillPlanRow>> BackfillPlan(Guid id, CancellationToken ct) => Ok(await service.BackfillPlanAsync(Actor, id, ct));
}
