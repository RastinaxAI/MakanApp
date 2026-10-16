using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingRealtimeAudienceResolver(MakanDbContext dbContext, IServiceScopeFactory scopeFactory)
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
            join user in dbContext.Users.AsNoTracking() on session.UserId equals user.Id
            where participant.ConversationId == conversationId &&
                  (conversation.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic ||
                   conversation.AcademicClassId != null && officialMembers.Any(m => m.UserId == session.UserId &&
                       m.ClassId == conversation.AcademicClassId && m.OrganizationId == conversation.OrganizationId)) &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  user.AccountStatus == UserAccountStatus.Active &&
                  session.RevokedAtUtc == null &&
                  session.ExpiresAtUtc > nowUtc &&
                  session.AbsoluteExpiresAtUtc > nowUtc &&
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
            select new { session.Id, session.UserId };

        var candidates = await query.Distinct().ToArrayAsync(cancellationToken);
        if (candidates.Length == 0) return [];

        // هر رویداد scope تازه می‌خواهد تا tracking نشست از رویداد قبلی، مجوز منقضی را زنده نگه ندارد.
        await using var authorizationScope = scopeFactory.CreateAsyncScope();
        var messagingService = authorizationScope.ServiceProvider.GetRequiredService<IMessagingService>();
        var authorized = new List<Guid>(candidates.Length);
        foreach (var session in candidates)
        {
            try
            {
                await messagingService.AuthorizeRealtimeSubscriptionAsync(
                    session.UserId, session.Id, conversationId, cancellationToken);
                authorized.Add(session.Id);
            }
            catch (Exception exception) when (exception is IdentityException or OrganizationException or GuardianException or MessagingException)
            {
                // رد مجوز یک نشست نباید مانع تحویل به بقیه شود؛ خطای فنی و cancellation بلعیده نمی‌شوند.
            }
        }
        return authorized;
    }
}
