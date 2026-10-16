using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/people/{userId:guid}/presence")]
[EnableRateLimiting("messaging-discovery")]
public sealed class PeoplePresenceController(MessagingEphemeralService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PresenceResult>(StatusCodes.Status200OK)]
    public Task<PresenceResult> Get(Guid userId, CancellationToken cancellationToken) =>
        service.GetPresenceAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), userId, cancellationToken);
}
