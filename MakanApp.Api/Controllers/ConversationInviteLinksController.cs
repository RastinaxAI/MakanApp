using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[EnableRateLimiting("messaging-send")]
public sealed class ConversationInviteLinksController(ConversationAdvancedManagementService service) : ControllerBase
{
    private Guid UserId => AuthenticatedSession.GetUserId(User);
    private Guid SessionId => AuthenticatedSession.GetSessionId(User);
    [HttpPost("api/v1/conversations/{conversationId:guid}/invite-links")]
    [ProducesResponseType<CreatedInviteLinkResult>(StatusCodes.Status200OK)]
    public Task<CreatedInviteLinkResult> Create(Guid conversationId, CreateInviteLinkCommand command, CancellationToken ct) =>
        service.CreateLinkAsync(UserId, SessionId, conversationId, command, HttpContext.TraceIdentifier, ct);
    [HttpGet("api/v1/conversations/{conversationId:guid}/invite-links")]
    [ProducesResponseType<InviteLinkPageResult>(StatusCodes.Status200OK)]
    public Task<InviteLinkPageResult> List(Guid conversationId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        service.GetLinksAsync(UserId, SessionId, conversationId, page, pageSize, ct);
    [HttpPost("api/v1/conversations/{conversationId:guid}/invite-links/{linkId:guid}/revoke")]
    [ProducesResponseType<InviteLinkResult>(StatusCodes.Status200OK)]
    public Task<InviteLinkResult> Revoke(Guid conversationId, Guid linkId, RevokeInviteLinkCommand command, CancellationToken ct) =>
        service.RevokeLinkAsync(UserId, SessionId, conversationId, linkId, command, HttpContext.TraceIdentifier, ct);
    [HttpPost("api/v1/messaging/invite-links/redeem")]
    [ProducesResponseType<InviteLinkRedemptionResult>(StatusCodes.Status200OK)]
    public Task<InviteLinkRedemptionResult> Redeem(RedeemInviteLinkCommand command, CancellationToken ct) =>
        service.RedeemLinkAsync(UserId, SessionId, command, HttpContext.TraceIdentifier, ct);
    [HttpGet("api/v1/conversations/{conversationId:guid}/join-requests")]
    [ProducesResponseType<ConversationJoinRequestPageResult>(StatusCodes.Status200OK)]
    public Task<ConversationJoinRequestPageResult> ListRequests(Guid conversationId, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        service.GetJoinRequestsAsync(UserId, SessionId, conversationId, page, pageSize, ct);
    [HttpGet("api/v1/messaging/join-requests")]
    [ProducesResponseType<ConversationJoinRequestPageResult>(StatusCodes.Status200OK)]
    public Task<ConversationJoinRequestPageResult> MyRequests(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        service.GetJoinRequestsAsync(UserId, SessionId, null, page, pageSize, ct);
    [HttpPost("api/v1/messaging/join-requests/{requestId:guid}/approve")]
    [ProducesResponseType<ConversationJoinRequestResult>(StatusCodes.Status200OK)]
    public Task<ConversationJoinRequestResult> Approve(Guid requestId, CompleteJoinRequestCommand command, CancellationToken ct) => Complete(requestId, ConversationJoinRequestStatus.Approved, command, ct);
    [HttpPost("api/v1/messaging/join-requests/{requestId:guid}/reject")]
    [ProducesResponseType<ConversationJoinRequestResult>(StatusCodes.Status200OK)]
    public Task<ConversationJoinRequestResult> Reject(Guid requestId, CompleteJoinRequestCommand command, CancellationToken ct) => Complete(requestId, ConversationJoinRequestStatus.Rejected, command, ct);
    [HttpPost("api/v1/messaging/join-requests/{requestId:guid}/cancel")]
    [ProducesResponseType<ConversationJoinRequestResult>(StatusCodes.Status200OK)]
    public Task<ConversationJoinRequestResult> Cancel(Guid requestId, CompleteJoinRequestCommand command, CancellationToken ct) => Complete(requestId, ConversationJoinRequestStatus.Cancelled, command, ct);
    private Task<ConversationJoinRequestResult> Complete(Guid requestId, ConversationJoinRequestStatus status, CompleteJoinRequestCommand command, CancellationToken ct) =>
        service.CompleteJoinRequestAsync(UserId, SessionId, requestId, status, command, HttpContext.TraceIdentifier, ct);
}
