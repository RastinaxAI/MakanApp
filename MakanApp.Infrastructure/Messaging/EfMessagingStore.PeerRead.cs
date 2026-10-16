using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ConversationPeerReadStateResult> GetPeerReadStateAsync(
        Guid userId, Guid conversationId, AccessContext accessContext,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == conversationId, cancellationToken) ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, participant, userId, accessContext, cancellationToken);
        if (conversation.Type != ConversationType.Direct)
            return new(conversationId, null, PeerReadVisibility.NotSupported, null);

        var peer = await GetVisibleReceiptPeerAsync(conversation, userId, accessContext, nowUtc, cancellationToken);
        if (peer is null) return new(conversationId, null, PeerReadVisibility.Hidden, null);

        var message = await ReceiptMessages(conversationId, userId, peer.UserId)
            .Where(m => m.Sequence <= peer.LastReadMessageSequence)
            .OrderByDescending(m => m.Sequence).FirstOrDefaultAsync(cancellationToken);
        var sequence = message?.Sequence ?? 0;
        return new(conversationId, sequence, PeerReadVisibility.Visible,
            MessagingChangeCursor.Encode(conversationId, sequence));
    }

    private async Task<ConversationParticipant?> GetVisibleReceiptPeerAsync(
        Conversation conversation, Guid userId, AccessContext accessContext,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (conversation.Type != ConversationType.Direct) return null;
        var peerId = conversation.GetDirectPair().Other(userId);
        if (await HasEffectiveUserBlockAsync(userId, peerId, cancellationToken)) return null;
        if (conversation.Scope == ConversationScope.Personal)
        {
            var ownPrivacy = await privacyResolver.ResolveAsync(peerId, userId, cancellationToken);
            if (!MessagingPrivacyPolicy.Allows(ownPrivacy.Settings.DirectMessagesAudience, ownPrivacy.TargetHasSavedRequester))
                return null;
        }
        var peer = await dbContext.ConversationParticipants.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ConversationId == conversation.Id && p.UserId == peerId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null, cancellationToken);
        if (peer is null) return null;
        var facts = await LoadEligibilityFactsAsync(userId, peerId, conversation.Scope,
            conversation.OrganizationId, accessContext, nowUtc, cancellationToken);
        return eligibilityPolicy.CanStartOrSend(facts, MessagingEligibilityOperation.Send) ? peer : null;
    }

    // رسید فقط به پیام خودِ درخواست‌کننده در تاریخچهٔ قابل مشاهدهٔ هر دو طرف اشاره می‌کند.
    private IQueryable<Message> ReceiptMessages(Guid conversationId, Guid senderId, Guid readerId) =>
        dbContext.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId &&
            m.SenderUserId == senderId && m.DeletedAtUtc == null &&
            !dbContext.MessageUserVisibilities.Any(v => v.MessageId == m.Id && (v.UserId == senderId || v.UserId == readerId)) &&
            dbContext.ConversationParticipants.Any(p => p.ConversationId == conversationId && p.UserId == senderId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null && m.Sequence >= p.HistoryVisibleFromSequence) &&
            dbContext.ConversationParticipants.Any(p => p.ConversationId == conversationId && p.UserId == readerId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null && m.Sequence >= p.HistoryVisibleFromSequence) &&
            !dbContext.ConversationUserStates.Any(s => s.ConversationId == conversationId &&
                (s.UserId == senderId || s.UserId == readerId) && s.HistoryHiddenThroughSequence >= m.Sequence));

    private async Task AppendPeerReadChangeAsync(
        Conversation conversation, ConversationParticipant reader, long previousReadSequence,
        AccessContext accessContext, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (conversation.Type != ConversationType.Direct) return;
        var peer = await GetVisibleReceiptPeerAsync(conversation, reader.UserId, accessContext, nowUtc, cancellationToken);
        if (peer is null) return;
        var message = await ReceiptMessages(conversation.Id, peer.UserId, reader.UserId)
            .Where(m => m.Sequence > previousReadSequence && m.Sequence <= reader.LastReadMessageSequence)
            .OrderByDescending(m => m.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (message is null) return;
        await AppendChangeAsync(conversation, MessagingChangeType.PeerReadStateChanged, message.Id,
            MessagingChangeCursor.Encode(conversation.Id, message.Sequence), nowUtc,
            null, peer.UserId, cancellationToken);
    }
}
