using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/contacts")]
public sealed class ContactsController(IMessagingDirectoryService directoryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserContactPageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserContactPageResult>> GetMine(
        [FromQuery] string? q,
        [FromQuery] bool favoritesOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await directoryService.GetContactsAsync(
            AuthenticatedSession.GetUserId(User),
            q,
            favoritesOnly,
            page,
            pageSize,
            cancellationToken));

    [HttpGet("recent")]
    [ProducesResponseType<RecentContactPageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RecentContactPageResult>> GetRecent(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await directoryService.GetRecentContactsAsync(
            AuthenticatedSession.GetUserId(User),
            page,
            pageSize,
            cancellationToken));

    [HttpPost]
    [ProducesResponseType<UserContactResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<UserContactResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserContactResult>> Save(
        SaveUserContactCommand command,
        CancellationToken cancellationToken)
    {
        var result = await directoryService.SaveContactAsync(
            AuthenticatedSession.GetUserId(User),
            command,
            cancellationToken);
        return result.AlreadyExisted
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPatch("{contactUserId:guid}")]
    [ProducesResponseType<UserContactResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserContactResult>> Update(
        Guid contactUserId,
        UpdateUserContactCommand command,
        CancellationToken cancellationToken) =>
        Ok(await directoryService.UpdateContactAsync(
            AuthenticatedSession.GetUserId(User),
            contactUserId,
            command,
            cancellationToken));

    [HttpDelete("{contactUserId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid contactUserId,
        CancellationToken cancellationToken)
    {
        await directoryService.DeleteContactAsync(
            AuthenticatedSession.GetUserId(User),
            contactUserId,
            cancellationToken);
        return NoContent();
    }
}
