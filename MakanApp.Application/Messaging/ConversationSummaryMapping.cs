using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public static class ConversationSummaryMapping
{
    public static ConversationSummaryResult Map(ConversationSummaryStoreRecord result, DateTime nowUtc) =>
        new(result.Conversation.Id, result.Conversation.Type, result.Conversation.Scope, result.Conversation.OrganizationId,
            result.Conversation.Type == ConversationType.Saved ? "پیام‌های ذخیره‌شده" : result.Conversation.Title,
            result.OtherParticipant is null ? null : new(result.OtherParticipant.UserId, result.OtherParticipant.Username,
                result.OtherParticipant.DisplayName, result.OtherParticipant.ProfilePhoto),
            result.LastMessagePreview, MessagingWireTime.FromStoredUtc(result.LastMessageAtUtc), result.LastMessageSequence,
            result.Participant.LastDeliveredMessageSequence, result.Participant.LastReadMessageSequence,
            result.UnreadCount, result.UnreadMentionCount, result.State?.IsArchived ?? false,
            result.State?.IsMutedAt(nowUtc) ?? false, result.State?.MuteMode ?? ConversationMuteMode.None,
            MessagingWireTime.FromStoredUtc(result.State?.MutedUntilUtc), result.State?.IsPinned ?? false,
            result.State?.IsManuallyUnread ?? false, result.Conversation.OrganizationId.HasValue && result.OrganizationName is not null
                ? new(result.Conversation.OrganizationId.Value, result.OrganizationName) : null)
        { Official = OfficialSpaceMetadata.From(result.Conversation), EffectiveCapabilities = result.EffectiveCapabilities, Photo = result.Photo };
}
