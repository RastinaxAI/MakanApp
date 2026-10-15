using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
public sealed class ConversationInvitationsController(ConversationInvitationService service) : ControllerBase
{
    [HttpPost("api/v1/conversations/{conversationId:guid}/invitations")]
    public async Task<ActionResult<ConversationInvitationResult>> Invite(Guid conversationId, CreateConversationInvitationCommand command, CancellationToken ct) =>
        Ok(await service.InviteAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), conversationId, command, HttpContext.TraceIdentifier, ct));
    [HttpGet("api/v1/messaging/invitations")]
    public async Task<ActionResult<IReadOnlyCollection<ConversationInvitationResult>>> List(CancellationToken ct) =>
        Ok(await service.ListAsync(AuthenticatedSession.GetUserId(User), ct));
    [HttpPost("api/v1/messaging/invitations/{invitationId:guid}/accept")]
    public Task<ActionResult<ConversationInvitationResult>> Accept(Guid invitationId, CancellationToken ct) => Complete(invitationId, ConversationInvitationStatus.Accepted, ct);
    [HttpPost("api/v1/messaging/invitations/{invitationId:guid}/decline")]
    public Task<ActionResult<ConversationInvitationResult>> Decline(Guid invitationId, CancellationToken ct) => Complete(invitationId, ConversationInvitationStatus.Declined, ct);
    [HttpPost("api/v1/messaging/invitations/{invitationId:guid}/revoke")]
    public Task<ActionResult<ConversationInvitationResult>> Revoke(Guid invitationId, CancellationToken ct) => Complete(invitationId, ConversationInvitationStatus.Revoked, ct);
    private async Task<ActionResult<ConversationInvitationResult>> Complete(Guid id, ConversationInvitationStatus status, CancellationToken ct) =>
        Ok(await service.CompleteAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, status, HttpContext.TraceIdentifier, ct));
}
