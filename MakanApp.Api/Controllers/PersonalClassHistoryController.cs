using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/personal-classes/{classId:guid}/history")]
public sealed class PersonalClassHistoryController(PersonalClassHistoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PersonalClassPage<PersonalClassHistoryItem>>> Read(Guid classId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken ct = default) =>
        Ok(await service.ReadAsync(new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier), classId, false, page, pageSize, ct));
}
