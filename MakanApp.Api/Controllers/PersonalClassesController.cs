using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/personal-classes")]
public sealed class PersonalClassesController(PersonalClassService service) : ControllerBase
{
    private Guid Actor => AuthenticatedSession.GetUserId(User);
    private Guid Session => AuthenticatedSession.GetSessionId(User);
    [HttpPost]
    public async Task<ActionResult<PersonalClassDto>> Create(CreatePersonalClassCommand command, CancellationToken ct) =>
        StatusCode(201, await service.CreateAsync(Actor, Session, command, ct));
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PersonalClassDto>>> List(CancellationToken ct) => Ok(await service.ListAsync(Actor, Session, ct));
    [HttpGet("{classId:guid}")]
    public async Task<ActionResult<PersonalClassDto>> Get(Guid classId, CancellationToken ct) => Ok(await service.GetAsync(Actor, Session, classId, ct));
    [HttpPost("{classId:guid}/archive")]
    public async Task<ActionResult<PersonalClassDto>> Archive(Guid classId, CancellationToken ct) => Ok(await service.ArchiveAsync(Actor, Session, classId, ct));
    [HttpPost("{classId:guid}/invitations")]
    public async Task<ActionResult<PersonalClassInvitationDto>> Invite(Guid classId, InvitePersonalClassStudentCommand command, CancellationToken ct) =>
        StatusCode(201, await service.InviteAsync(Actor, Session, classId, command, ct));
    [HttpGet("invitations")]
    public async Task<ActionResult<IReadOnlyList<PersonalClassInvitationDto>>> Invitations(CancellationToken ct) => Ok(await service.InvitationsAsync(Actor, Session, ct));
    [HttpPost("invitations/{invitationId:guid}/accept")]
    public async Task<ActionResult<PersonalClassDto>> Accept(Guid invitationId, CancellationToken ct) => Ok(await service.AcceptAsync(Actor, Session, invitationId, ct));
    [HttpGet("{classId:guid}/participants")]
    public async Task<ActionResult<IReadOnlyList<PersonalClassParticipantDto>>> Participants(Guid classId, CancellationToken ct) =>
        Ok(await service.ParticipantsAsync(Actor, Session, classId, ct));
    [HttpDelete("{classId:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> End(Guid classId, Guid participantId, CancellationToken ct)
    { await service.EndParticipantAsync(Actor, Session, classId, participantId, ct); return NoContent(); }
}
