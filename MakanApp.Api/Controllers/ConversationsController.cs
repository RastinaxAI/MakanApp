using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/conversations")]
public sealed class ConversationsController(
    IMessagingService messagingService,
    IConversationManagementService managementService) : ControllerBase
{
    [HttpGet("{conversationId:guid}/read-states")]
    [ProducesResponseType<ConversationPeerReadStateResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationPeerReadStateResult>> GetReadStates(
        Guid conversationId, CancellationToken cancellationToken) =>
        Ok(await messagingService.GetPeerReadStateAsync(
            AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            conversationId, cancellationToken));

    [HttpPost("groups")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ManagedConversationResult>> CreateGroup(
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken) =>
        CreatedOrExisting(await managementService.CreateGroupAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken));

    [HttpPost("channels")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ManagedConversationResult>> CreateChannel(
        CreateManagedConversationCommand command,
        CancellationToken cancellationToken) =>
        CreatedOrExisting(await managementService.CreateChannelAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken));

    [HttpPost("direct")]
    [ProducesResponseType<DirectConversationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<DirectConversationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<DirectConversationResult>> StartDirect(
        StartDirectConversationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await messagingService.StartOrGetDirectConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return result.AlreadyExisted
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("saved")]
    [ProducesResponseType<SavedConversationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<SavedConversationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SavedConversationResult>> GetSaved(
        CancellationToken cancellationToken)
    {
        var result = await messagingService.GetOrCreateSavedConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken);
        return result.AlreadyExisted
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ConversationSummaryResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ConversationSummaryResult>>> GetMine(
        CancellationToken cancellationToken,
        [FromQuery] ConversationListFilter filter = ConversationListFilter.All,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] Guid? folderId = null,
        [FromQuery] ConversationScope? scope = null) =>
        Ok(await messagingService.GetMyConversationsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            new GetConversationListQuery(filter, page, pageSize, search, folderId, scope),
            cancellationToken));

    [HttpGet("{conversationId:guid}/summary")]
    [ProducesResponseType<ConversationSummaryResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationSummaryResult>> GetSummary(Guid conversationId, CancellationToken cancellationToken) =>
        Ok(await messagingService.GetConversationSummaryAsync(AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User), conversationId, cancellationToken));

    [HttpGet("discover")]
    [ProducesResponseType<DiscoverableConversationPageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DiscoverableConversationPageResult>> Discover(
        CancellationToken cancellationToken,
        [FromQuery] ConversationType? type,
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] ConversationScope? scope = null) =>
        Ok(await managementService.DiscoverAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            new DiscoverConversationsQuery(type, query, page, pageSize, scope),
            cancellationToken));

    [HttpGet("{conversationId:guid}/messages")]
    [ProducesResponseType<ConversationMessagePageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationMessagePageResult>> GetMessages(
        Guid conversationId,
        [FromQuery] long? beforeSequence,
        [FromQuery] int? limit,
        [FromQuery] Guid? topicId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.GetConversationMessagesAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            new GetConversationMessagesQuery(beforeSequence, limit, topicId),
            cancellationToken));

    [HttpPost("{conversationId:guid}/messages")]
    [EnableRateLimiting("messaging-send")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<MessageReceiptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageReceiptResult>> SendMessage(
        Guid conversationId,
        SendMessageCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.SendMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));

    [HttpPatch("{conversationId:guid}/messages/{messageId:guid}")]
    [EnableRateLimiting("messaging-send")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<MessageMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageMutationResult>> EditMessage(
        Guid conversationId,
        Guid messageId,
        EditMessageCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.EditMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            command,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/messages/{messageId:guid}")]
    [ProducesResponseType<MessageMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageMutationResult>> DeleteMessage(
        Guid conversationId,
        Guid messageId,
        [FromQuery] string expectedVersion,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.DeleteMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            expectedVersion,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/messages/{messageId:guid}/for-everyone")]
    [ProducesResponseType<MessageMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageMutationResult>> DeleteMessageForEveryone(
        Guid conversationId,
        Guid messageId,
        [FromQuery] string expectedVersion,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.DeleteMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            expectedVersion,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/messages/{messageId:guid}/for-me")]
    [ProducesResponseType<DeleteMessageForMeResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DeleteMessageForMeResult>> DeleteMessageForMe(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.DeleteMessageForMeAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/messages/{messageId:guid}/forward")]
    [EnableRateLimiting("messaging-forward")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<MessageReceiptResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageReceiptResult>> ForwardMessage(
        Guid conversationId,
        Guid messageId,
        ForwardMessageCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.ForwardMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            command,
            cancellationToken));

    [HttpPost("{conversationId:guid}/messages/{messageId:guid}/reactions")]
    [EnableRateLimiting("messaging-reaction")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<MessageReactionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MessageReactionResult>> AddReaction(
        Guid conversationId,
        Guid messageId,
        AddReactionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.AddReactionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            command,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/messages/{messageId:guid}/reactions/{reaction}")]
    [EnableRateLimiting("messaging-reaction")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveReaction(
        Guid conversationId,
        Guid messageId,
        MessageReactionType reaction,
        CancellationToken cancellationToken)
    {
        await messagingService.RemoveReactionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            reaction,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{conversationId:guid}/pins/{messageId:guid}")]
    [ProducesResponseType<ConversationPinResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationPinResult>> PinMessage(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.PinMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/pins/{messageId:guid}")]
    [ProducesResponseType<ConversationPinResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationPinResult>> UnpinMessage(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.UnpinMessageAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            messageId,
            cancellationToken));

    [HttpGet("{conversationId:guid}/media")]
    [ProducesResponseType<ConversationMediaPageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationMediaPageResult>> GetMedia(
        Guid conversationId,
        [FromQuery] MessageKind? kind,
        [FromQuery] long? beforeSequence,
        [FromQuery] int? limit,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.GetConversationMediaAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            new GetConversationMediaQuery(kind, beforeSequence, limit),
            cancellationToken));

    [HttpGet("{conversationId:guid}/changes")]
    [ProducesResponseType<ConversationChangePageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationChangePageResult>> GetChanges(
        Guid conversationId,
        [FromQuery] string? afterCursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.GetConversationChangesAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            new GetConversationChangesQuery(afterCursor, limit),
            cancellationToken));

    [HttpPost("{conversationId:guid}/read")]
    [ProducesResponseType<ConversationCursorStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationCursorStateResult>> MarkRead(
        Guid conversationId,
        AdvanceConversationCursorCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.MarkConversationReadAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));

    [HttpPost("{conversationId:guid}/delivered")]
    [ProducesResponseType<ConversationCursorStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationCursorStateResult>> AcknowledgeDelivery(
        Guid conversationId,
        AdvanceConversationCursorCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.AcknowledgeConversationDeliveryAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));

    [HttpGet("{conversationId:guid}")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> GetDetails(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.GetDetailsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/members")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> AddMember(
        Guid conversationId,
        AddConversationMemberCommand command,
        CancellationToken cancellationToken) =>
        Ok(await managementService.AddMemberAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));

    [HttpGet("{conversationId:guid}/members")]
    [ProducesResponseType<ConversationParticipantPageResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationParticipantPageResult>> GetMembers(
        Guid conversationId,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30) =>
        Ok(await managementService.GetMembersAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            page,
            pageSize,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/members/{targetUserId:guid}")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> RemoveMember(
        Guid conversationId,
        Guid targetUserId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.RemoveMemberAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            targetUserId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/leave")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> Leave(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.LeaveAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPatch("{conversationId:guid}/members/{targetUserId:guid}/role")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> ChangeMemberRole(
        Guid conversationId,
        Guid targetUserId,
        ChangeConversationMemberRoleCommand command,
        CancellationToken cancellationToken) =>
        Ok(await managementService.ChangeMemberRoleAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            targetUserId,
            command,
            cancellationToken));

    [HttpPatch("{conversationId:guid}/members/{targetUserId:guid}/permissions")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> UpdateMemberPermissions(
        Guid conversationId,
        Guid targetUserId,
        UpdateConversationPermissionsCommand command,
        CancellationToken cancellationToken) =>
        Ok(await managementService.UpdateMemberPermissionsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            targetUserId,
            command,
            cancellationToken));

    [HttpPatch("{conversationId:guid}/settings")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> UpdateSettings(
        Guid conversationId,
        UpdateConversationSettingsCommand command,
        CancellationToken cancellationToken) =>
        Ok(await managementService.UpdateSettingsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));

    [HttpPost("{conversationId:guid}/join")]
    [ProducesResponseType<ManagedConversationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ManagedConversationResult>> JoinPublic(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.JoinPublicAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/ownership-transfers")]
    [ProducesResponseType<OwnershipTransferResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OwnershipTransferResult>> StartOwnershipTransfer(
        Guid conversationId,
        StartOwnershipTransferCommand command,
        CancellationToken cancellationToken)
    {
        var result = await managementService.StartOwnershipTransferAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{conversationId:guid}/ownership-transfers/{transferId:guid}/accept")]
    [ProducesResponseType<OwnershipTransferResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OwnershipTransferResult>> AcceptOwnershipTransfer(
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.AcceptOwnershipTransferAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            transferId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/ownership-transfers/{transferId:guid}/decline")]
    [ProducesResponseType<OwnershipTransferResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OwnershipTransferResult>> DeclineOwnershipTransfer(
        Guid conversationId,
        Guid transferId,
        CancellationToken cancellationToken) =>
        Ok(await managementService.DeclineOwnershipTransferAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            transferId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/archive")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> Archive(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.ArchiveConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/archive")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> Unarchive(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.UnarchiveConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/state/pin")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> PinConversation(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.PinConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/state/pin")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> UnpinConversation(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.UnpinConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPut("{conversationId:guid}/mute")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> SetMute(
        Guid conversationId,
        SetConversationMuteCommand command,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.SetConversationMuteAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            command,
            cancellationToken));

    [HttpDelete("{conversationId:guid}/mute")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> Unmute(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.UnmuteConversationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/mark-unread")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> MarkUnread(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.MarkConversationUnreadAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    [HttpPost("{conversationId:guid}/clear-history")]
    [ProducesResponseType<ConversationUserStateResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationUserStateResult>> ClearHistory(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        Ok(await messagingService.ClearConversationHistoryAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            conversationId,
            cancellationToken));

    private ActionResult<ManagedConversationResult> CreatedOrExisting(
        ManagedConversationResult result) =>
        result.AlreadyExisted
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
}
