using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingRealtimeAudienceResolver(MakanDbContext dbContext)
    : IMessagingRealtimeAudienceResolver
{
    public async Task<IReadOnlyCollection<Guid>> GetActiveSessionIdsAsync(
        Guid conversationId,
        Guid? audienceUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return [];
        }

        var officialMembers = MessengerRelationshipQuery.ClassMembers(dbContext);
        var query =
            from participant in dbContext.ConversationParticipants.AsNoTracking()
            join session in dbContext.UserSessions.AsNoTracking()
                on participant.UserId equals session.UserId
            where participant.ConversationId == conversationId &&
                  (conversation.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
                   conversation.AcademicClassId != null && officialMembers.Any(m => m.UserId == session.UserId &&
                       m.ClassId == conversation.AcademicClassId && m.OrganizationId == conversation.OrganizationId)) &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  session.RevokedAtUtc == null &&
                  session.ExpiresAtUtc > nowUtc &&
                  (!audienceUserId.HasValue || participant.UserId == audienceUserId.Value) &&
                  (conversation.Scope == ConversationScope.Personal &&
                   session.SelectedMembershipId == null ||
                   conversation.Scope == ConversationScope.Organization &&
                   session.SelectedMembershipId != null &&
                   dbContext.Memberships.Any(membership =>
                       membership.Id == session.SelectedMembershipId &&
                       membership.UserId == session.UserId &&
                       membership.OrganizationId == conversation.OrganizationId &&
                       membership.Status == MembershipStatus.Active &&
                       membership.EndedAtUtc == null))
            select session.Id;

        return await query.Distinct().ToArrayAsync(cancellationToken);
    }
}
