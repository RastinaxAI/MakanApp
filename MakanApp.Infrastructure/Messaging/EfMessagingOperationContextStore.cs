using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed class EfMessagingOperationContextStore(MakanDbContext db) : IMessagingOperationContextStore
{
    public Task<bool> IsPersonalConversationAsync(Guid conversationId, CancellationToken ct) =>
        db.Conversations.AnyAsync(c => c.Id == conversationId && c.Scope == ConversationScope.Personal && c.OrganizationId == null, ct);

    public Task<bool> IsPersonalInvitationAsync(Guid userId, Guid invitationId, CancellationToken ct) =>
        (from invitation in db.ConversationInvitations
         join conversation in db.Conversations on invitation.ConversationId equals conversation.Id
         where invitation.Id == invitationId && (invitation.TargetUserId == userId || invitation.InviterUserId == userId) &&
             conversation.Scope == ConversationScope.Personal && conversation.OrganizationId == null
         select invitation.Id).AnyAsync(ct);

    public Task<bool> IsPersonalJoinRequestAsync(Guid userId, Guid requestId, CancellationToken ct) =>
        (from request in db.ConversationJoinRequests
         join conversation in db.Conversations on request.ConversationId equals conversation.Id
         where request.Id == requestId && conversation.Scope == ConversationScope.Personal && conversation.OrganizationId == null &&
             (request.UserId == userId || db.ConversationParticipants.Any(p => p.ConversationId == conversation.Id && p.UserId == userId &&
                 p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null))
         select request.Id).AnyAsync(ct);

    public Task<bool> IsPersonalInviteLinkAsync(string token, CancellationToken ct)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        return (from link in db.ConversationInviteLinks
                join conversation in db.Conversations on link.ConversationId equals conversation.Id
                where link.TokenHash == hash && conversation.Scope == ConversationScope.Personal && conversation.OrganizationId == null
                select link.Id).AnyAsync(ct);
    }

    public Task<bool> HasActiveAccountSessionAsync(Guid userId, Guid sessionId, DateTime nowUtc, CancellationToken ct) =>
        (from session in db.UserSessions
         join user in db.Users on session.UserId equals user.Id
         where session.Id == sessionId && session.UserId == userId && user.AccountStatus == UserAccountStatus.Active &&
             session.RevokedAtUtc == null && session.ExpiresAtUtc > nowUtc && session.AbsoluteExpiresAtUtc > nowUtc
         select session.Id).AnyAsync(ct);

    public Task<bool> IsPersonalParticipantAsync(Guid userId, Guid conversationId, CancellationToken ct) =>
        (from conversation in db.Conversations
         join participant in db.ConversationParticipants on conversation.Id equals participant.ConversationId
         where conversation.Id == conversationId && conversation.Scope == ConversationScope.Personal &&
             conversation.OrganizationId == null && participant.UserId == userId &&
             participant.Status == ConversationParticipantStatus.Active && participant.EndedAtUtc == null
         select conversation.Id).AnyAsync(ct);
}
