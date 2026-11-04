using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/organization/invitations")]
public sealed class SchoolSentInvitationsController(SchoolInvitationReadService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SchoolPage<SchoolSentInvitationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolPage<SchoolSentInvitationDto>>> Read([FromQuery] SchoolInvitationQuery query, CancellationToken ct) =>
        Ok(await service.ReadAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), query, ct));
}
