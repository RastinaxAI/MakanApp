using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize(Policy = PlatformPermissionKeys.PersonalClassesRead)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/admin/v1/personal-classes/{classId:guid}/memberships")]
public sealed class PlatformPersonalClassMembershipsController(PersonalClassAdminMembershipService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PersonalClassPage<PlatformPersonalClassMembershipDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonalClassPage<PlatformPersonalClassMembershipDto>>> Read(Guid classId,
        [FromQuery] PlatformPersonalClassMembershipQuery query, CancellationToken ct) =>
        Ok(await service.ReadAsync(new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            HttpContext.TraceIdentifier), classId, query, ct));
}
