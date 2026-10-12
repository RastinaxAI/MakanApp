using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/messaging/privacy")]
public sealed class MessagingPrivacyController(IMessagingDirectoryService directoryService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MessagingPrivacySettingsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagingPrivacySettingsResult>> Get(
        CancellationToken cancellationToken) =>
        Ok(await directoryService.GetPrivacySettingsAsync(
            AuthenticatedSession.GetUserId(User),
            cancellationToken));

    [HttpPut]
    [ProducesResponseType<MessagingPrivacySettingsResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessagingPrivacySettingsResult>> Update(
        UpdateMessagingPrivacyCommand command,
        CancellationToken cancellationToken) =>
        Ok(await directoryService.UpdatePrivacySettingsAsync(
            AuthenticatedSession.GetUserId(User),
            command,
            cancellationToken));
}
