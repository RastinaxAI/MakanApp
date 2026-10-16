using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore : IConversationAdvancedManagementStore
{
    private static void RequireAdvancedPermission(ConversationParticipant actor, ConversationAdminPermission permission)
    {
        if (!actor.CanManage(permission)) throw PermissionRequired();
    }

    private async Task<(Conversation Conversation, ConversationParticipant Actor)> LoadAdvancedManagerAsync(Guid userId, Guid conversationId,
        AccessContext context, ConversationAdminPermission permission, CancellationToken ct)
    {
        var conversation = await dbContext.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, ct) ?? throw ConversationNotFound();
        var actor = await dbContext.ConversationParticipants.AsNoTracking().SingleOrDefaultAsync(p => p.ConversationId == conversationId &&
            p.UserId == userId && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null, ct) ?? throw ConversationNotFound();
        await EnsureManagedContextAsync(conversation, userId, context, false, ct);
        RequireAdvancedPermission(actor, permission);
        return (conversation, actor);
    }

    private async Task EnsureNotBannedAsync(Guid conversationId, Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        if (await dbContext.ConversationMemberRestrictions.AnyAsync(r => r.ConversationId == conversationId && r.UserId == userId &&
            r.IsBanned && (r.UntilUtc == null || r.UntilUtc > nowUtc), ct))
            throw new MessagingException(MessagingErrorCodes.MemberRestricted, "عضویت یا عملیات جاری مجاز نیست.");
    }

    private async Task EnsureCapabilityNotRestrictedAsync(Guid conversationId, Guid userId, GroupMemberCapability capability, DateTime nowUtc, CancellationToken ct)
    {
        if (await dbContext.ConversationMemberRestrictions.AnyAsync(r => r.ConversationId == conversationId && r.UserId == userId &&
            (r.IsBanned || (r.DeniedCapabilities & capability) != 0) && (r.UntilUtc == null || r.UntilUtc > nowUtc), ct))
            throw new MessagingException(MessagingErrorCodes.MemberRestricted, "عملیات عضو محدود شده است.");
    }

    private async Task AuditAdvancedAsync(Guid userId, Guid sessionId, string action, string resourceType, string resourceId,
        string reason, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        var personId = await dbContext.Users.Where(u => u.Id == userId).Select(u => u.PersonId).SingleAsync(ct);
        dbContext.AdminAuditLogs.Add(AdminAuditLog.Create(userId, personId, sessionId, action, resourceType, resourceId,
            null, reason, nowUtc, correlationId, AdminAuditResult.Succeeded, null, null));
    }

    public async Task<ConversationInfoResult> UpdateInfoAsync(Guid actorId, Guid sessionId, Guid conversationId,
        UpdateConversationInfoCommand command, byte[] expectedVersion, AccessContext context, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var (conversation, actor) = await LoadManagedForMutationAsync(actorId, conversationId, context, ct);
        nowUtc = await EnsureAdvancedSessionAsync(actorId, sessionId, context, ct);
        RequireAdvancedPermission(actor, ConversationAdminPermission.ChangeInfo);
        if (!conversation.RowVersion.SequenceEqual(expectedVersion)) throw StateConflict();
        dbContext.Entry(conversation).Property(c => c.RowVersion).OriginalValue = expectedVersion;
        conversation.UpdateInformation(command.Title, command.Description);
        await AuditAdvancedAsync(actorId, sessionId, "messaging.conversation.info", "Conversation", conversation.Id.ToString("D"),
            "تغییر مشخصات گفتگو", correlationId, nowUtc, ct);
        await SaveManagementAsync(ct);
        await AppendChangeAsync(conversation, MessagingChangeType.ConversationChanged, conversation.Id, VersionOf(conversation.RowVersion), nowUtc, actorId, null, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal();
        return new(conversation.Id, conversation.Title!, conversation.Description, Convert.ToBase64String(conversation.RowVersion));
    }

    public async Task<MemberRestrictionPageResult> GetRestrictionsAsync(Guid actorId, Guid conversationId, AccessContext context,
        int skip, int take, DateTime nowUtc, CancellationToken ct)
    {
        await LoadAdvancedManagerAsync(actorId, conversationId, context, ConversationAdminPermission.BanMembers, ct);
        var rows = await dbContext.ConversationMemberRestrictions.AsNoTracking().Where(r => r.ConversationId == conversationId)
            .OrderBy(r => r.UserId).Skip(skip).Take(take + 1).ToArrayAsync(ct);
        return new(rows.Take(take).Select(r => MapRestriction(r, nowUtc)).ToArray(), rows.Length > take);
    }

    public async Task<MemberRestrictionResult> SetRestrictionAsync(Guid actorId, Guid sessionId, Guid conversationId, Guid targetUserId,
        SetMemberRestrictionCommand command, byte[]? expectedVersion, AccessContext context, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var (conversation, actor) = await LoadManagedForMutationAsync(actorId, conversationId, context, ct);
        nowUtc = await EnsureAdvancedSessionAsync(actorId, sessionId, context, ct);
        RequireAdvancedPermission(actor, ConversationAdminPermission.BanMembers);
        var target = await dbContext.ConversationParticipants.Where(p => p.ConversationId == conversationId && p.UserId == targetUserId)
            .OrderByDescending(p => p.JoinedAtUtc).ThenByDescending(p => p.Id).FirstOrDefaultAsync(ct) ?? throw ParticipantNotFound();
        if (!actor.CanRestrictMember(target)) throw PermissionRequired();
        var restriction = await dbContext.ConversationMemberRestrictions.SingleOrDefaultAsync(r => r.ConversationId == conversationId && r.UserId == targetUserId, ct);
        if (restriction is null)
        {
            if (expectedVersion is not null) throw StateConflict();
            restriction = ConversationMemberRestriction.Create(conversationId, targetUserId); dbContext.ConversationMemberRestrictions.Add(restriction);
        }
        else
        {
            if (expectedVersion is null || !restriction.RowVersion.SequenceEqual(expectedVersion)) throw StateConflict();
            dbContext.Entry(restriction).Property(r => r.RowVersion).OriginalValue = expectedVersion;
        }
        restriction.Update(command.IsBanned, command.DeniedCapabilities, command.UntilUtc, nowUtc);
        var removed = command.IsBanned && target.IsActive;
        if (removed) target.Remove(actorId, nowUtc);
        await AuditAdvancedAsync(actorId, sessionId, "messaging.member.restriction", "ConversationMember", conversationId.ToString("D") + "/" + targetUserId.ToString("D"),
            command.Reason.Trim(), correlationId, nowUtc, ct);
        await SaveManagementAsync(ct);
        if (removed) await AppendChangeAsync(conversation, MessagingChangeType.ParticipantChanged, target.Id, VersionOf(target.RowVersion), nowUtc, actorId, null, ct);
        await transaction.CommitAsync(ct); if (removed) outboxWakeSignal.Signal();
        return MapRestriction(restriction, nowUtc);
    }

    private static MemberRestrictionResult MapRestriction(ConversationMemberRestriction r, DateTime nowUtc) =>
        new(r.ConversationId, r.UserId, r.IsBanned, r.DeniedCapabilities, r.UntilUtc is null ? null : AdvancedUtc(r.UntilUtc.Value),
            r.IsEffective(nowUtc), Convert.ToBase64String(r.RowVersion));
    // datetime2 بازیابی‌شده Kind ندارد؛ دادهٔ معتبرِ ذخیره‌شده UTC است و ساعت جابه‌جا نمی‌شود.
    private static DateTime AdvancedUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static MessagingException LinkUnavailable() => new(MessagingErrorCodes.InviteLinkUnavailable, "لینک در دسترس نیست.");
}
