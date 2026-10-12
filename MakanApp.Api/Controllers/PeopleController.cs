using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting("messaging-discovery")]
[Route("api/v1/people")]
public sealed class PeopleController(IMessagingDirectoryService directoryService) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType<PeopleSearchPageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PeopleSearchPageResult>> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await directoryService.SearchPeopleAsync(
            AuthenticatedSession.GetUserId(User),
            q,
            page,
            pageSize,
            cancellationToken));

    [HttpPost("resolve")]
    [ProducesResponseType<PeopleResolveResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PeopleResolveResult>> Resolve(
        ResolvePersonCommand command,
        CancellationToken cancellationToken) =>
        Ok(await directoryService.ResolvePersonAsync(
            AuthenticatedSession.GetUserId(User),
            command,
            cancellationToken));

    [HttpGet("{userId:guid}")]
    [ProducesResponseType<MessagingPublicProfileResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagingPublicProfileResult>> GetProfile(
        Guid userId,
        CancellationToken cancellationToken) =>
        Ok(await directoryService.GetPublicProfileAsync(
            AuthenticatedSession.GetUserId(User),
            userId,
            cancellationToken));
}
