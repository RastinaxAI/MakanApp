using MakanApp.Application.Platform;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Authorize(Policy = PlatformPermissionKeys.MessagingReadMetadata)]
[Route("api/admin/v1/messaging/conversations")]
public sealed class PlatformMessagingMetadataController(
    IPlatformEducationSafetyService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PlatformConversationMetadataPage>> GetConversations(
        [FromQuery] Guid? organizationId,
        [FromQuery] ConversationType? type,
        [FromQuery] ConversationScope? scope,
        [FromQuery] ConversationVisibility? visibility,
        [FromQuery] ConversationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetConversationsAsync(
            new PlatformConversationMetadataQuery(
                organizationId,
                type,
                scope,
                visibility,
                status,
                page,
                pageSize),
            cancellationToken));
}
