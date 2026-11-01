using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

internal static class AccountInboxAccessQuery
{
    public static IQueryable<Conversation> Conversations(MakanDbContext db, Guid userId)
    {
        var grants = OfficialSpaceAccessQuery.Conversations(db);
        return db.Conversations.AsNoTracking().Where(c => c.Status == ConversationStatus.Active &&
            db.Users.Any(u => u.Id == userId && u.AccountStatus == UserAccountStatus.Active &&
                u.PersonId != null && u.Username != null && u.ProfileCompletedAtUtc != null) &&
            db.ConversationParticipants.Any(p => p.ConversationId == c.Id && p.UserId == userId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null) &&
            (c.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic || grants.Any(g => g.ConversationId == c.Id && g.UserId == userId)) &&
            (c.Scope == ConversationScope.Personal && (c.Type != ConversationType.Saved || c.SavedOwnerUserId == userId) ||
             c.Scope == ConversationScope.Organization && db.Organizations.Any(o => o.Id == c.OrganizationId && o.Status == OrganizationStatus.Active) &&
             db.Memberships.Any(m => m.UserId == userId && m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
                 db.RoleAssignments.Any(r => r.MembershipId == m.Id && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null))));
    }

    public static async Task<AccessContext?> ResolveReadContextAsync(MakanDbContext db, Guid userId, Guid sessionId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await Conversations(db, userId).SingleOrDefaultAsync(c => c.Id == conversationId, ct);
        if (conversation is null) return null;
        if (conversation.Scope == ConversationScope.Personal)
            return new(userId, userId, sessionId, WorkspaceType.Personal, null, null, null, null);
        var membership = await (from m in db.Memberships.AsNoTracking()
            join role in db.RoleAssignments.AsNoTracking() on m.Id equals role.MembershipId
            where m.UserId == userId && m.OrganizationId == conversation.OrganizationId && m.Status == MembershipStatus.Active && m.EndedAtUtc == null &&
                role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null
            orderby m.Id, role.Role
            select new { m.Id, role.Role }).FirstOrDefaultAsync(ct);
        return membership is null ? null : new(userId, userId, sessionId, WorkspaceType.Organization,
            conversation.OrganizationId, membership.Id, membership.Role, null);
    }
}
