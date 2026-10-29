using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/organization/members")]
public sealed class OrganizationMembersController(SchoolReadService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SchoolPage<SchoolMemberResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolPage<SchoolMemberResult>>> Get([FromQuery] SchoolMemberQuery query, CancellationToken ct) =>
        Ok(await service.MembersAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), query, ct));
}
