using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize(Policy = PlatformPermissionKeys.PersonalClassesRead), Route("api/admin/v1/personal-classes/{classId:guid}/history")]
public sealed class PlatformPersonalClassHistoryController(PersonalClassHistoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PersonalClassPage<PersonalClassHistoryItem>>> Read(Guid classId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.ReadAsync(new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier), classId, true, page, pageSize, ct));
}
