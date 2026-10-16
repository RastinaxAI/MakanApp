using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Messaging;

public sealed record SafeMessagingIdentityRecord(
    Guid UserId,
    string Username,
    string DisplayName);

public sealed record DirectConversationStoreResult(
    Conversation Conversation,
    SafeMessagingIdentityRecord OtherParticipant,
    bool AlreadyExisted);

public sealed record SavedConversationStoreResult(
    Conversation Conversation,
    bool AlreadyExisted);

public sealed record ConversationSummaryStoreRecord(
    Conversation Conversation,
    ConversationParticipant Participant,
    SafeMessagingIdentityRecord? OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageAtUtc,
    long? LastMessageSequence,
    int UnreadCount,
    int UnreadMentionCount,
    ConversationUserState? State,
    string? OrganizationName);

public sealed record MessageReplySummaryStoreRecord(
    Guid MessageId,
    Message? Message,
    bool IsUnavailable);

public sealed record MessageAttachmentStoreRecord(
    MessageAttachment Attachment,
    FileAsset FileAsset);

public sealed record MessageReactionSummaryStoreRecord(
    MessageReactionType Reaction,
    int Count,
    bool ReactedByCurrentUser);

public sealed record ConversationMessageStoreRecord(
    Message Message,
    SafeMessagingIdentityRecord Sender,
    MessageReplySummaryStoreRecord? Reply,
    IReadOnlyCollection<MessageReactionSummaryStoreRecord> Reactions,
    IReadOnlyCollection<SafeMessagingIdentityRecord> Mentions,
    IReadOnlyCollection<MessageAttachmentStoreRecord> Attachments,
    bool IsPinned);

public sealed record ConversationMessagePageStoreResult(
    IReadOnlyCollection<ConversationMessageStoreRecord> Messages,
    long? NextBeforeSequence);

public enum MessageSafetyPurpose { Message = 0, TopicMetadata = 1, ConversationMetadata = 2 }
public sealed record ScheduledSendClaim(Guid ScheduledMessageId, Guid Token, byte[] PayloadHash);

public sealed record SendMessageStoreCommand(
    Guid ClientMessageId,
    MessageKind Kind,
    string? Text,
    IReadOnlyCollection<Guid> AttachmentIds,
    Guid? ReplyToMessageId,
    IReadOnlyCollection<Guid> MentionedUserIds,
    Guid? ForwardedFromMessageId = null,
    bool AllowSourceAttachments = false,
    Guid? ContactUserId = null,
    Guid? TopicId = null,
    MessageSafetyPurpose SafetyPurpose = MessageSafetyPurpose.Message,
    bool IsSilent = false,
    ScheduledSendClaim? ScheduledClaim = null);

public sealed record MessageSafetyPreparationStoreResult(
    Message? ExistingMessage,
    ContentSafetyDecision? ExistingSafetyDecision,
    bool RequiresEvaluation,
    IReadOnlyCollection<ContentSafetyAttachment> Attachments);

public sealed record SafetyIncidentStoreResult(
    Message? ExistingMessage,
    ContentSafetyDecision Decision);

public sealed record ConversationMediaStoreRecord(
    Message Message,
    MessageAttachment Attachment,
    FileAsset FileAsset);

public sealed record ConversationMediaPageStoreResult(
    IReadOnlyCollection<ConversationMediaStoreRecord> Items,
    long? NextBeforeSequence);

public sealed record ConversationChangePageStoreResult(
    IReadOnlyCollection<MessagingChangeEvent> Changes,
    long NextChangeSequence,
    bool HasMore,
    IReadOnlyDictionary<Guid, long>? PeerReadSequences = null);

public sealed record ConversationCursorStoreResult(
    ConversationParticipant Participant,
    int UnreadCount);

public interface IMessagingStore
{
    Task<ConversationPeerReadStateResult> GetPeerReadStateAsync(
        Guid userId, Guid conversationId, AccessContext accessContext,
        DateTime nowUtc, CancellationToken cancellationToken);

    Task<DirectConversationStoreResult> StartOrGetDirectConversationAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<SavedConversationStoreResult> GetOrCreateSavedConversationAsync(
        Guid userId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConversationSummaryStoreRecord>> GetMyConversationsAsync(
        Guid userId,
        AccessContext accessContext,
        ConversationListFilter filter,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken,
        Guid? folderId = null);

    Task<ConversationMessagePageStoreResult> GetConversationMessagesAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long? beforeSequence,
        int limit,
        CancellationToken cancellationToken,
        Guid? topicId = null);

    Task<Message> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        bool safetyEvaluated,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<MessageSafetyPreparationStoreResult> PrepareMessageSafetyAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        byte[] requestPayloadHash,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<SafetyIncidentStoreResult> RecordSafetyIncidentAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        byte[] requestPayloadHash,
        ContentSafetyResult result,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<SendMessageStoreCommand> PrepareForwardMessageSafetyAsync(
        Guid userId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        AccessContext accessContext,
        Guid destinationConversationId,
        Guid clientMessageId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<SendMessageStoreCommand> PrepareEditMessageSafetyAsync(Guid userId, Guid conversationId, Guid messageId,
        AccessContext access, string? text, byte[] expectedVersion, DateTime nowUtc, CancellationToken ct);

    Task<Message> EditMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        string? text,
        byte[] expectedVersion,
        bool safetyEvaluated,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<Message> DeleteMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<(MessageUserVisibility Visibility, bool AlreadyHidden)> DeleteMessageForMeAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationUserState> SetConversationArchivedAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        bool archived,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationUserState> SetConversationPinnedAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        bool pinned,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationUserState> SetConversationMuteAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        ConversationMuteMode mode,
        DateTime? mutedUntilUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationUserState> MarkConversationUnreadAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationUserState> ClearConversationHistoryAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<Message> ForwardMessageAsync(
        Guid userId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        AccessContext accessContext,
        Guid destinationConversationId,
        SendMessageStoreCommand command,
        bool safetyEvaluated,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<MessageReaction> AddReactionAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        MessageReactionType reaction,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task RemoveReactionAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        MessageReactionType reaction,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationPin> PinMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationPin?> UnpinMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationMediaPageStoreResult> GetConversationMediaAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        MessageKind? kind,
        long? beforeSequence,
        int limit,
        CancellationToken cancellationToken);

    Task<ConversationChangePageStoreResult> GetConversationChangesAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long afterChangeSequence,
        int limit,
        CancellationToken cancellationToken);

    Task<ConversationCursorStoreResult> AdvanceReadCursorAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long upToMessageSequence,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationCursorStoreResult> AdvanceDeliveryCursorAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long upToMessageSequence,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task EnsureRealtimeAccessAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        CancellationToken cancellationToken);
}

public interface IMessagingRealtimeNotifier
{
    Task NotifyConversationChangedAsync(
        MessagingRealtimeNotification notification,
        Guid? audienceUserId,
        CancellationToken cancellationToken);
}

public interface IMessagingRealtimeAudienceResolver
{
    Task<IReadOnlyCollection<Guid>> GetActiveSessionIdsAsync(
        MessagingRealtimeNotification notification,
        Guid? audienceUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}

public interface IMessagingService
{
    Task<ConversationPeerReadStateResult> GetPeerReadStateAsync(
        Guid userId, Guid sessionId, Guid conversationId, CancellationToken cancellationToken);

    Task<SavedConversationResult> GetOrCreateSavedConversationAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<DirectConversationResult> StartOrGetDirectConversationAsync(
        Guid userId,
        Guid sessionId,
        StartDirectConversationCommand command,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConversationSummaryResult>> GetMyConversationsAsync(
        Guid userId,
        Guid sessionId,
        GetConversationListQuery query,
        CancellationToken cancellationToken);

    Task<ConversationMessagePageResult> GetConversationMessagesAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetConversationMessagesQuery query,
        CancellationToken cancellationToken);

    Task<MessageReceiptResult> SendMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageReceiptResult> SendTextMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendTextMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageMutationResult> EditMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        EditMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageMutationResult> DeleteMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        string expectedVersion,
        CancellationToken cancellationToken);

    Task<DeleteMessageForMeResult> DeleteMessageForMeAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> ArchiveConversationAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> UnarchiveConversationAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> PinConversationAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> UnpinConversationAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> SetConversationMuteAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SetConversationMuteCommand command,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> UnmuteConversationAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> MarkConversationUnreadAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<ConversationUserStateResult> ClearConversationHistoryAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<MessageReceiptResult> ForwardMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        ForwardMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageReactionResult> AddReactionAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        AddReactionCommand command,
        CancellationToken cancellationToken);

    Task RemoveReactionAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        MessageReactionType reaction,
        CancellationToken cancellationToken);

    Task<ConversationPinResult> PinMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    Task<ConversationPinResult> UnpinMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    Task<ConversationMediaPageResult> GetConversationMediaAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetConversationMediaQuery query,
        CancellationToken cancellationToken);

    Task<ConversationChangePageResult> GetConversationChangesAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetConversationChangesQuery query,
        CancellationToken cancellationToken);

    Task<ConversationCursorStateResult> MarkConversationReadAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        AdvanceConversationCursorCommand command,
        CancellationToken cancellationToken);

    Task<ConversationCursorStateResult> AcknowledgeConversationDeliveryAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        AdvanceConversationCursorCommand command,
        CancellationToken cancellationToken);

    Task AuthorizeRealtimeSubscriptionAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        CancellationToken cancellationToken);
}
