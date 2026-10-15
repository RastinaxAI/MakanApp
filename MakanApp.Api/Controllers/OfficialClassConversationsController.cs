using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[Route("api/v1/organization/classes/{classId:guid}/conversation")]
public sealed class OfficialClassConversationsController(OfficialClassConversationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OfficialClassConversationResult>> Resolve(Guid classId, CancellationToken ct) =>
        Ok(await service.ResolveAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), classId, ct));
}
