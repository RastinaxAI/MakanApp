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
using System.Runtime.CompilerServices;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingRealtimeAudienceResolver(MakanDbContext dbContext, IServiceScopeFactory scopeFactory)
    : IMessagingRealtimeAudienceResolver
{
    public const int CandidatePageSize = 128;

    public async IAsyncEnumerable<Guid> GetActiveSessionIdsAsync(
        MessagingRealtimeNotification notification,
        Guid? audienceUserId,
        DateTime nowUtc,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversationId = notification.ConversationId;
        if (!MessagingChangeCursor.TryDecode(notification.Cursor, conversationId, out var changeSequence) || changeSequence < 1)
            throw new InvalidOperationException("نشانگر رویداد realtime با گفتگو سازگار نیست.");
        var precedingCursor = MessagingChangeCursor.Encode(conversationId, changeSequence - 1);
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            yield break;
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

        Guid? afterSessionId = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageQuery = query.Distinct();
            if (afterSessionId is Guid cursor)
                pageQuery = pageQuery.Where(session => session.Id.CompareTo(cursor) > 0);
            var candidates = await pageQuery.OrderBy(session => session.Id).Take(CandidatePageSize)
                .ToArrayAsync(cancellationToken);
            if (candidates.Length == 0) yield break;
            afterSessionId = candidates[^1].Id;

            // reader صفحه بسته است؛ scope تازه اجازه نمی‌دهد tracking صفحهٔ قبلی مجوز را زنده نگه دارد.
            await using var authorizationScope = scopeFactory.CreateAsyncScope();
            var messagingService = authorizationScope.ServiceProvider.GetRequiredService<IMessagingService>();
            foreach (var session in candidates)
            {
                var authorized = false;
                try
                {
                    // همان Delta، مرز FromJoin/Clear/DeleteForMe و audience خصوصی خود رویداد را نیز می‌سنجد.
                    var visible = await messagingService.GetConversationChangesAsync(
                        session.UserId, session.Id, conversationId,
                        new GetConversationChangesQuery(precedingCursor, 1), cancellationToken);
                    authorized = visible.Changes.Any(change => change.Cursor == notification.Cursor &&
                        change.Type == notification.Type && change.ResourceId == notification.ResourceId);
                }
                catch (Exception exception) when (exception is IdentityException or OrganizationException or GuardianException or MessagingException)
                {
                    // رد مجوز یک نشست نباید مانع بقیه شود؛ خطای فنی و cancellation بلعیده نمی‌شوند.
                }
                if (authorized) yield return session.Id;
            }
        }
    }
}
