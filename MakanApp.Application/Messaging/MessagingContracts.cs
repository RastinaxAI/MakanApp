using MakanApp.Domain.Messaging;
using MakanApp.Application.Identity;

namespace MakanApp.Application.Messaging;

public sealed record StartDirectConversationCommand(
    Guid TargetUserId,
    ConversationScope Scope);

public sealed record SafeMessagingIdentityResult(
    Guid UserId,
    string Username,
    string DisplayName,
    ProfilePhotoResult? ProfilePhoto = null);

public sealed record DirectConversationResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    Guid? OrganizationId,
    SafeMessagingIdentityResult OtherParticipant,
    DateTime CreatedAtUtc,
    bool AlreadyExisted);

public sealed record SavedConversationResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    DateTime CreatedAtUtc,
    bool AlreadyExisted);

public enum ConversationListFilter
{
    All = 1,
    Direct = 2,
    Group = 3,
    Channel = 4,
    Unread = 5,
    Archived = 6,
    Saved = 7
}

public sealed record GetConversationListQuery(
    ConversationListFilter Filter = ConversationListFilter.All,
    int Page = 1,
    int PageSize = 30,
    string? Search = null,
    Guid? FolderId = null,
    ConversationScope? Scope = null);

public sealed record ConversationOrganizationBadgeResult(
    Guid OrganizationId,
    string OrganizationName);

public sealed record ConversationSummaryResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    Guid? OrganizationId,
    string? Title,
    SafeMessagingIdentityResult? OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageAtUtc,
    long? LastMessageSequence,
    long LastDeliveredMessageSequence,
    long LastReadMessageSequence,
    int UnreadCount,
    int UnreadMentionCount,
    bool IsArchived,
    bool IsMuted,
    ConversationMuteMode MuteMode,
    DateTime? MutedUntilUtc,
    bool IsPinned,
    bool IsManuallyUnread,
    ConversationOrganizationBadgeResult? Organization)
{
    public OfficialSpaceMetadata? Official { get; init; }
    public bool IsOfficial => Official is not null;
}

public sealed record GetConversationMessagesQuery(
    long? BeforeSequence,
    int? Limit,
    Guid? TopicId = null);

public sealed record MessageReplySummaryResult(
    Guid MessageId,
    Guid? SenderUserId,
    MessageKind? Kind,
    string? Text,
    bool IsDeleted,
    bool IsUnavailable);

public sealed record MessageReactionSummaryResult(
    MessageReactionType Reaction,
    int Count,
    bool ReactedByCurrentUser);

public sealed record MessageAttachmentResult(
    Guid FileAssetId,
    MessageKind Kind,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);

public sealed record ContactCardResult(
    Guid UserId,
    string Username,
    string DisplayName);

public sealed record ConversationMessageResult(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderUserId,
    SafeMessagingIdentityResult Sender,
    Guid ClientMessageId,
    long Sequence,
    MessageKind? Kind,
    string? Text,
    DateTime SentAtUtc,
    DateTime? EditedAtUtc,
    DateTime? DeletedAtUtc,
    bool IsEdited,
    bool IsDeleted,
    MessageReplySummaryResult? Reply,
    bool IsForwarded,
    IReadOnlyCollection<MessageReactionSummaryResult> Reactions,
    IReadOnlyCollection<SafeMessagingIdentityResult> Mentions,
    IReadOnlyCollection<MessageAttachmentResult> Attachments,
    ContactCardResult? ContactCard,
    bool IsPinned,
    string Version,
    Guid? TopicId = null,
    bool IsSilent = false)
{
    public string OriginKind { get; init; } = "User";
    public AcademicActivityCardResult? AcademicActivity { get; init; }
}

public sealed record ConversationMessagePageResult(
    IReadOnlyCollection<ConversationMessageResult> Messages,
    long? NextBeforeSequence);

public sealed class SendMessageCommand
{
    public Guid ClientMessageId { get; init; }
    public MessageKind Kind { get; init; } = MessageKind.Text;
    public string? Text { get; init; }
    public IReadOnlyCollection<Guid>? AttachmentIds { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public IReadOnlyCollection<Guid>? MentionedUserIds { get; init; }
    public Guid? ContactUserId { get; init; }
    public Guid? TopicId { get; init; }
    public bool IsSilent { get; init; }
}

// این قرارداد برای سازگاری کلاینت‌های STEP 7A باقی می‌ماند؛ مسیر اجرا مشترک است.
public sealed record SendTextMessageCommand(Guid ClientMessageId, string Text);

public sealed record EditMessageCommand(string? Text, string ExpectedVersion);

public sealed record ForwardMessageCommand(
    Guid DestinationConversationId,
    Guid ClientMessageId,
    Guid? DestinationTopicId = null,
    bool IsSilent = false);

public sealed record AddReactionCommand(MessageReactionType Reaction);

public sealed record MessageReactionResult(
    Guid MessageId,
    MessageReactionType Reaction,
    DateTime CreatedAtUtc);

public sealed record ConversationPinResult(
    Guid ConversationId,
    Guid MessageId,
    Guid PinnedByUserId,
    DateTime PinnedAtUtc,
    bool IsPinned);

public sealed record MessageMutationResult(
    Guid MessageId,
    long Sequence,
    DateTime? EditedAtUtc,
    DateTime? DeletedAtUtc,
    bool IsDeleted,
    string Version);

public sealed record DeleteMessageForMeResult(
    Guid MessageId,
    Guid ConversationId,
    DateTime HiddenAtUtc,
    bool AlreadyHidden);

public sealed record SetConversationMuteCommand(
    ConversationMuteMode Mode,
    DateTime? MutedUntilUtc);

public sealed record ConversationUserStateResult(
    Guid ConversationId,
    bool IsArchived,
    bool IsMuted,
    ConversationMuteMode MuteMode,
    DateTime? MutedUntilUtc,
    bool IsPinned,
    bool IsManuallyUnread,
    long HistoryHiddenThroughSequence,
    DateTime UpdatedAtUtc,
    string Version);

public sealed record GetConversationMediaQuery(
    MessageKind? Kind,
    long? BeforeSequence,
    int? Limit);

public sealed record ConversationMediaItemResult(
    Guid MessageId,
    long Sequence,
    Guid SenderUserId,
    DateTime SentAtUtc,
    MessageAttachmentResult Attachment);

public sealed record ConversationMediaPageResult(
    IReadOnlyCollection<ConversationMediaItemResult> Items,
    long? NextBeforeSequence);

public sealed record GetConversationChangesQuery(
    string? AfterCursor,
    int? Limit);

public sealed record ConversationChangeResult(
    Guid ChangeId,
    MessagingChangeType Type,
    Guid ResourceId,
    string? ResourceVersion,
    string Cursor,
    DateTime OccurredAtUtc,
    Guid? ActorUserId,
    int PayloadVersion,
    long? PeerReadThroughSequence = null);

public sealed record ConversationChangePageResult(
    IReadOnlyCollection<ConversationChangeResult> Changes,
    string NextCursor,
    bool HasMore);

public sealed record AdvanceConversationCursorCommand(long UpToMessageSequence);

public sealed record ConversationCursorStateResult(
    Guid ConversationId,
    long LastDeliveredMessageSequence,
    long LastReadMessageSequence,
    int UnreadCount,
    DateTime? UpdatedAtUtc,
    string Version);

public sealed record MessagingRealtimeNotification(
    Guid ConversationId,
    MessagingChangeType Type,
    Guid ResourceId,
    string? ResourceVersion,
    string Cursor,
    DateTime OccurredAtUtc,
    int PayloadVersion,
    long? PeerReadThroughSequence = null,
    string? ContextRevision = null);

public enum PeerReadVisibility
{
    Visible = 1,
    Hidden = 2,
    NotSupported = 3
}

public sealed record ConversationPeerReadStateResult(
    Guid ConversationId,
    long? PeerReadThroughSequence,
    PeerReadVisibility Visibility,
    string? Version);

public static class MessagingRealtimeEventNames
{
    public const string ConversationChanged = "ConversationChanged";
}

public enum MessageReceiptStatus
{
    Sent = 1
}

public sealed record MessageReceiptResult(
    Guid MessageId,
    Guid ConversationId,
    Guid ClientMessageId,
    long Sequence,
    MessageKind Kind,
    DateTime SentAtUtc,
    MessageReceiptStatus Status,
    string Version);
