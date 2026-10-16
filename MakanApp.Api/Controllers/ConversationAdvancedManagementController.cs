using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[RequestSizeLimit(64 * 1024)]
[EnableRateLimiting("messaging-send")]
public sealed class ConversationAdvancedManagementController(ConversationAdvancedManagementService service) : ControllerBase
{
    [HttpPatch("api/v1/conversations/{conversationId:guid}/info")]
    [ProducesResponseType<ConversationInfoResult>(StatusCodes.Status200OK)]
    public Task<ConversationInfoResult> UpdateInfo(Guid conversationId, UpdateConversationInfoCommand command, CancellationToken ct) =>
        service.UpdateInfoAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, command, HttpContext.TraceIdentifier, ct);
    [HttpGet("api/v1/conversations/{conversationId:guid}/member-restrictions")]
    [ProducesResponseType<MemberRestrictionPageResult>(StatusCodes.Status200OK)]
    public Task<MemberRestrictionPageResult> GetRestrictions(Guid conversationId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        service.GetRestrictionsAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, page, pageSize, ct);
    [HttpPut("api/v1/conversations/{conversationId:guid}/members/{userId:guid}/restriction")]
    [ProducesResponseType<MemberRestrictionResult>(StatusCodes.Status200OK)]
    public Task<MemberRestrictionResult> Restrict(Guid conversationId, Guid userId, SetMemberRestrictionCommand command, CancellationToken ct) =>
        service.SetRestrictionAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, userId, command, HttpContext.TraceIdentifier, ct);
    [HttpGet("api/v1/conversations/{conversationId:guid}/me/restriction")]
    [ProducesResponseType<MemberRestrictionResult>(StatusCodes.Status200OK)]
    public Task<MemberRestrictionResult> GetOwnRestriction(Guid conversationId, CancellationToken ct) =>
        service.GetOwnRestrictionAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, ct);
}
