using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IMessagingEphemeralAccessStore
{
    public async Task AuthorizeTypingAsync(Guid userId, Guid conversationId, AccessContext context, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants.AsNoTracking().SingleOrDefaultAsync(p => p.ConversationId == conversationId &&
            p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null, cancellationToken)
            ?? throw ConversationNotFound();
        if (conversation.Type == ConversationType.Saved) throw ConversationNotFound();
        await EnsureCanSendAdvancedAsync(conversation, participant, userId, context, MessageKind.Text, nowUtc, cancellationToken);
    }

    public async Task AuthorizeTypingRecipientAsync(Guid userId, Guid sourceUserId, Guid conversationId, AccessContext context,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants.AsNoTracking().SingleOrDefaultAsync(p => p.ConversationId == conversationId &&
            p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(conversation, participant, userId, context, cancellationToken);
        if (sourceUserId != userId && conversation.Type == ConversationType.Direct &&
            await GetVisibleReceiptPeerAsync(conversation, userId, context, nowUtc, cancellationToken) is null)
            throw ConversationNotFound();
    }

    public async Task<PresenceVisibility> GetPresenceVisibilityAsync(Guid userId, Guid targetUserId, AccessContext context,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (targetUserId == userId) return new(true, true);
        if (targetUserId == Guid.Empty) return new(false, false);
        var pair = DirectUserPair.Create(userId, targetUserId);
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Type == ConversationType.Direct &&
            c.DirectUserLowId == pair.LowerUserId && c.DirectUserHighId == pair.HigherUserId &&
            c.OrganizationId == context.OrganizationId &&
            (context.WorkspaceType == WorkspaceType.Personal && c.Scope == ConversationScope.Personal ||
             context.WorkspaceType == WorkspaceType.Organization && c.Scope == ConversationScope.Organization), cancellationToken);
        if (conversation is null) return new(false, false);
        var participant = await dbContext.ConversationParticipants.AsNoTracking().SingleOrDefaultAsync(p => p.ConversationId == conversation.Id &&
            p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null, cancellationToken);
        if (participant is null) return new(false, false);
        await EnsureCanReadAdvancedAsync(conversation, participant, userId, context, cancellationToken);
        if (await GetVisibleReceiptPeerAsync(conversation, userId, context, nowUtc, cancellationToken) is null) return new(false, false);
        var privacy = await privacyResolver.ResolveAsync(userId, targetUserId, cancellationToken);
        return new(MessagingPrivacyPolicy.Allows(privacy.Settings.OnlineAudience, privacy.TargetHasSavedRequester),
            MessagingPrivacyPolicy.Allows(privacy.Settings.LastSeenAudience, privacy.TargetHasSavedRequester));
    }

    public async Task<IReadOnlySet<Guid>> GetActiveSessionsAsync(IReadOnlyCollection<Guid> sessionIds, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0) return new HashSet<Guid>();
        return (await (from session in dbContext.UserSessions.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on session.UserId equals user.Id
            where sessionIds.Contains(session.Id) && session.RevokedAtUtc == null && session.ExpiresAtUtc > nowUtc &&
                session.AbsoluteExpiresAtUtc > nowUtc && user.AccountStatus == UserAccountStatus.Active
            select session.Id).ToArrayAsync(cancellationToken)).ToHashSet();
    }
}
