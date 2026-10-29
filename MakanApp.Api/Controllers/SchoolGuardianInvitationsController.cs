using MakanApp.Api.Authentication;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/organization")]
public sealed class SchoolGuardianInvitationsController(SchoolGuardianInvitationService service) : ControllerBase
{
    [HttpPost("people/{learnerId:guid}/guardian-invitations")]
    [EnableRateLimiting("school-parent-invite")]
    [ProducesResponseType<SchoolGuardianInvitationResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<SchoolGuardianInvitationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolGuardianInvitationResult>> Create(Guid learnerId, CreateSchoolGuardianInvitationCommand command, CancellationToken ct)
    {
        var result = await service.CreateAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), learnerId, command, ct);
        return StatusCode(result.Replayed ? 200 : 201, result);
    }
    [HttpPost("guardian-invitations/{invitationId:guid}/revoke")]
    [ProducesResponseType<SchoolGuardianInvitationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolGuardianInvitationResult>> Revoke(Guid invitationId, OnboardingStateCommand command, CancellationToken ct) =>
        Ok(await service.RevokeAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), invitationId, command, ct));
}
