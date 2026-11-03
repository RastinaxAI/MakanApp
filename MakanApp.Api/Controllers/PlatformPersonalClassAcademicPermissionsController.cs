using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize(Policy = PlatformPermissionKeys.PersonalClassesAcademicPermissions),
 Route("api/admin/v1/personal-classes/{classId:guid}/academic-permissions")]
public sealed class PlatformPersonalClassAcademicPermissionsController(PersonalClassAcademicPermissionService service) : ControllerBase
{
    private PersonalClassActor Actor => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpPut("{membershipId:guid}")]
    public async Task<ActionResult<PersonalClassAcademicPermissionDto>> Set(Guid classId, Guid membershipId,
        SetPersonalClassAcademicPermissionCommand command, CancellationToken ct) => Ok(await service.SetAsync(Actor, classId, membershipId, command, true, ct));
    [HttpGet]
    public async Task<ActionResult<PersonalClassPage<PersonalClassAcademicPermissionDto>>> List(Guid classId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.ListAsync(Actor, classId, true, page, pageSize, ct));
}
