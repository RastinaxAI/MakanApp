using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private async Task<IReadOnlyDictionary<Guid, ConversationEffectiveCapabilities>> ReadCapabilitiesBatchAsync(
        IReadOnlyCollection<Conversation> conversations, IReadOnlyCollection<ConversationParticipant> participants, Guid userId, CancellationToken ct)
    {
        if (conversations.Count == 0) return new Dictionary<Guid, ConversationEffectiveCapabilities>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var ids = conversations.Select(c => c.Id).ToArray();
        var available = await dbContext.Users.AnyAsync(u => u.Id == userId && u.AccountStatus == UserAccountStatus.Active &&
            u.ProfileCompletedAtUtc != null && u.PersonId != null && u.Username != null &&
            (u.MessagingRestrictedUntilUtc == null || u.MessagingRestrictedUntilUtc <= now), ct);
        var restrictions = await dbContext.ConversationMemberRestrictions.AsNoTracking().Where(r => ids.Contains(r.ConversationId) &&
            r.UserId == userId && (r.UntilUtc == null || r.UntilUtc > now)).ToDictionaryAsync(r => r.ConversationId, ct);
        var grants = await OfficialSpaceAccessQuery.Conversations(dbContext).Where(g => ids.Contains(g.ConversationId) && g.UserId == userId).ToArrayAsync(ct);
        return conversations.ToDictionary(c => c.Id, c =>
        {
            var participant = participants.SingleOrDefault(p => p.ConversationId == c.Id && p.UserId == userId && p.IsActive);
            if (participant is null) return ConversationEffectiveCapabilities.None;
            var restriction = restrictions.GetValueOrDefault(c.Id);
            return ConversationCapabilityPolicy.Evaluate(new(c.Type, c.ManagementPolicy, c.Status, true, participant.Role,
                participant.AdminPermissions, c.MemberCapabilities, grants.Any(g => g.ConversationId == c.Id && g.CanManage),
                grants.Any(g => g.ConversationId == c.Id && g.CanPublish), !available, restriction?.IsBanned == true,
                restriction?.DeniedCapabilities ?? GroupMemberCapability.None));
        });
    }

    private async Task<ConversationEffectiveCapabilities> ReadCapabilitiesAsync(Conversation conversation, Guid userId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var participant = await dbContext.ConversationParticipants.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ConversationId == conversation.Id && p.UserId == userId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null, ct);
        if (participant is null) return ConversationEffectiveCapabilities.None;
        var actorAvailable = await dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == userId &&
            u.AccountStatus == UserAccountStatus.Active && u.ProfileCompletedAtUtc != null && u.PersonId != null && u.Username != null &&
            (u.MessagingRestrictedUntilUtc == null || u.MessagingRestrictedUntilUtc <= now), ct);
        var restriction = await dbContext.ConversationMemberRestrictions.AsNoTracking()
            .SingleOrDefaultAsync(r => r.ConversationId == conversation.Id && r.UserId == userId &&
                (r.UntilUtc == null || r.UntilUtc > now), ct);
        var official = OfficialSpaceAccessQuery.Conversations(dbContext).Where(g => g.ConversationId == conversation.Id && g.UserId == userId);
        var manage = conversation.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic && await official.AnyAsync(g => g.CanManage, ct);
        var publish = conversation.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic && await official.AnyAsync(g => g.CanPublish, ct);
        return ConversationCapabilityPolicy.Evaluate(new(conversation.Type, conversation.ManagementPolicy, conversation.Status,
            true, participant.Role, participant.AdminPermissions, conversation.MemberCapabilities, manage, publish,
            !actorAvailable, restriction?.IsBanned == true, restriction?.DeniedCapabilities ?? GroupMemberCapability.None));
    }

    private async Task RequireContentManagementAsync(Conversation conversation, ConversationParticipant actor, Guid actorId,
        ConversationAdminPermission permission, CancellationToken ct)
    {
        if (conversation.ManagementPolicy != ConversationManagementPolicy.SystemManagedAcademic)
        {
            RequireAdvancedPermission(actor, permission);
            return;
        }
        const ConversationAdminPermission allowed = ConversationAdminPermission.ChangeInfo | ConversationAdminPermission.DeleteMessages |
            ConversationAdminPermission.PinMessages | ConversationAdminPermission.ManageTopics;
        if ((permission & ~allowed) != 0 || !await OfficialSpaceAccessQuery.Conversations(dbContext)
            .AnyAsync(g => g.ConversationId == conversation.Id && g.UserId == actorId && g.CanManage, ct))
            throw PermissionRequired();
    }
}
