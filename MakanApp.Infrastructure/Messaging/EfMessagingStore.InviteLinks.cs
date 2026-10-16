using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<CreatedInviteLinkResult> CreateLinkAsync(Guid actorId, Guid sessionId, Guid conversationId, CreateInviteLinkCommand command,
        AccessContext context, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var (conversation, actor) = await LoadManagedForMutationAsync(actorId, conversationId, context, ct);
        nowUtc = await EnsureAdvancedSessionAsync(actorId, sessionId, context, ct);
        RequireAdvancedPermission(actor, ConversationAdminPermission.ManageInvites);
        await EnsureCapabilityNotRestrictedAsync(conversationId, actorId, GroupMemberCapability.InviteMembers, nowUtc, ct);
        await EnsureActorCanMessageAsync(actorId, nowUtc, ct);
        await EnsurePersonalCreationSafetyAsync(actorId, conversation.Scope, conversation.Type, conversation.Visibility, ct);
        var existing = await dbContext.ConversationInviteLinks.SingleOrDefaultAsync(l => l.Id == command.ClientLinkId, ct);
        if (existing is not null)
        {
            if (existing.ConversationId != conversationId || existing.CreatedByUserId != actorId) throw LinkUnavailable();
            if (existing.ExpiresAtUtc != command.ExpiresAtUtc || existing.MaximumUses != command.MaximumUses || existing.RequiresApproval != command.RequiresApproval) throw StateConflict();
            await transaction.CommitAsync(ct); return new(MapLink(existing), null, true);
        }
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        ConversationInviteLink link;
        try
        {
            link = ConversationInviteLink.Create(command.ClientLinkId, conversationId, actorId, HashInviteToken(token), command.ExpiresAtUtc,
                command.MaximumUses, command.RequiresApproval, nowUtc,
                conversation.Scope == ConversationScope.Organization ? context.MembershipId : null,
                conversation.Scope == ConversationScope.Organization ? context.ActiveRole : null);
        }
        catch (ArgumentException ex) { throw new MessagingException(MessagingErrorCodes.AdvancedManagementInvalid, "تنظیم لینک معتبر نیست.", ex); }
        dbContext.ConversationInviteLinks.Add(link);
        await AuditAdvancedAsync(actorId, sessionId, "messaging.invite-link.created", "ConversationInviteLink", link.Id.ToString("D"), "ایجاد لینک دعوت", correlationId, nowUtc, ct);
        await SaveManagementAsync(ct); await transaction.CommitAsync(ct);
        // توکن فقط همین بار برمی‌گردد؛ hash، audit و list راه بازیابی متن آن نیستند.
        return new(MapLink(link), token, false);
    }

    public async Task<InviteLinkPageResult> GetLinksAsync(Guid actorId, Guid conversationId, AccessContext context, int skip, int take, CancellationToken ct)
    {
        await LoadAdvancedManagerAsync(actorId, conversationId, context, ConversationAdminPermission.ManageInvites, ct);
        var rows = await dbContext.ConversationInviteLinks.AsNoTracking().Where(l => l.ConversationId == conversationId)
            .OrderByDescending(l => l.CreatedAtUtc).ThenBy(l => l.Id).Skip(skip).Take(take + 1).ToArrayAsync(ct);
        return new(rows.Take(take).Select(MapLink).ToArray(), rows.Length > take);
    }

    public async Task<InviteLinkResult> RevokeLinkAsync(Guid actorId, Guid sessionId, Guid conversationId, Guid linkId, byte[] expectedVersion,
        AccessContext context, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var (_, actor) = await LoadManagedForMutationAsync(actorId, conversationId, context, ct);
        nowUtc = await EnsureAdvancedSessionAsync(actorId, sessionId, context, ct);
        RequireAdvancedPermission(actor, ConversationAdminPermission.ManageInvites);
        var link = await dbContext.ConversationInviteLinks.SingleOrDefaultAsync(l => l.Id == linkId && l.ConversationId == conversationId, ct) ?? throw LinkUnavailable();
        if (link.RevokedAtUtc is not null) { await transaction.CommitAsync(ct); return MapLink(link); }
        if (!link.RowVersion.SequenceEqual(expectedVersion)) throw StateConflict();
        dbContext.Entry(link).Property(l => l.RowVersion).OriginalValue = expectedVersion;
        link.Revoke(nowUtc);
        await AuditAdvancedAsync(actorId, sessionId, "messaging.invite-link.revoked", "ConversationInviteLink", link.Id.ToString("D"), "لغو لینک دعوت", correlationId, nowUtc, ct);
        await SaveManagementAsync(ct); await transaction.CommitAsync(ct); return MapLink(link);
    }

    public async Task<InviteLinkRedemptionResult> RedeemLinkAsync(Guid actorId, Guid sessionId, string token, AccessContext context,
        string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        var hash = HashInviteToken(token);
        var identity = await dbContext.ConversationInviteLinks.AsNoTracking().Where(l => l.TokenHash == hash)
            .Select(l => new { l.Id, l.ConversationId }).SingleOrDefaultAsync(ct) ?? throw LinkUnavailable();
        await using var transaction = await BeginManagementTransactionAsync(ct);
        var conversation = await LockConversationAsync(identity.ConversationId, ct) ?? throw LinkUnavailable();
        nowUtc = await EnsureAdvancedSessionAsync(actorId, sessionId, context, ct);
        var link = await dbContext.ConversationInviteLinks.SingleAsync(l => l.Id == identity.Id, ct);
        await EnsureLinkCurrentAsync(conversation, link, actorId, context, nowUtc, ct);
        if (await LoadActiveParticipantAsync(conversation.Id, actorId, ct) is not null)
        { await transaction.CommitAsync(ct); return new(conversation.Id, true, null); }
        await EnsureLinkTargetEligibleAsync(conversation, link, actorId, nowUtc, ct);
        if (link.UsedCount >= link.MaximumUses) throw LinkUnavailable();
        if (link.RequiresApproval)
        {
            var pending = await dbContext.ConversationJoinRequests.SingleOrDefaultAsync(r => r.ConversationId == conversation.Id && r.UserId == actorId &&
                r.Status == ConversationJoinRequestStatus.Pending, ct);
            if (pending is not null && pending.ExpiresAtUtc > nowUtc)
            { await transaction.CommitAsync(ct); return new(conversation.Id, false, MapJoinRequest(pending, nowUtc)); }
            if (pending is not null) { pending.Complete(ConversationJoinRequestStatus.Expired, nowUtc); await SaveManagementAsync(ct); }
            var request = ConversationJoinRequest.Create(conversation.Id, link.Id, actorId, AdvancedUtc(link.ExpiresAtUtc), nowUtc,
                conversation.Scope == ConversationScope.Organization ? context.MembershipId : null,
                conversation.Scope == ConversationScope.Organization ? context.ActiveRole : null);
            dbContext.ConversationJoinRequests.Add(request);
            await AuditAdvancedAsync(actorId, sessionId, "messaging.join-request.created", "ConversationJoinRequest", request.Id.ToString("D"), "درخواست صریح عضویت", correlationId, nowUtc, ct);
            await SaveManagementAsync(ct); await transaction.CommitAsync(ct);
            return new(conversation.Id, false, MapJoinRequest(request, nowUtc));
        }
        await AddLinkMemberAsync(conversation, link, actorId, actorId, sessionId, correlationId, nowUtc, ct);
        await transaction.CommitAsync(ct); outboxWakeSignal.Signal();
        return new(conversation.Id, false, null);
    }

    private async Task EnsureLinkCurrentAsync(Conversation conversation, ConversationInviteLink link, Guid joinerId, AccessContext context, DateTime nowUtc, CancellationToken ct)
    {
        if (conversation.Type is not ConversationType.Group and not ConversationType.Channel || conversation.ManagementPolicy != ConversationManagementPolicy.UserManaged ||
            conversation.Status != ConversationStatus.Active || !link.IsOpen(nowUtc) || !await CanAccessScopeAsync(conversation, joinerId, context, ct) ||
            !await HasActiveActorContextAsync(joinerId, conversation.Scope, conversation.OrganizationId, context, ct)) throw LinkUnavailable();
        var creator = await LoadActiveParticipantAsync(conversation.Id, link.CreatedByUserId, ct);
        if (creator is null || !creator.CanManage(ConversationAdminPermission.ManageInvites)) throw LinkUnavailable();
        var creatorContext = CreatorContext(conversation, link);
        if (!await HasActiveActorContextAsync(link.CreatedByUserId, conversation.Scope, conversation.OrganizationId, creatorContext, ct)) throw LinkUnavailable();
        try
        {
            await EnsureActorCanMessageAsync(link.CreatedByUserId, nowUtc, ct);
            await EnsurePersonalCreationSafetyAsync(link.CreatedByUserId, conversation.Scope, conversation.Type, conversation.Visibility, ct);
            await EnsureCapabilityNotRestrictedAsync(conversation.Id, link.CreatedByUserId, GroupMemberCapability.InviteMembers, nowUtc, ct);
            await EnsureNotBannedAsync(conversation.Id, joinerId, nowUtc, ct);
        }
        catch (MessagingException) { throw LinkUnavailable(); }
    }

    private async Task EnsureLinkTargetEligibleAsync(Conversation conversation, ConversationInviteLink link, Guid targetUserId, DateTime nowUtc, CancellationToken ct)
    {
        try
        {
            await EnsureEligibleTargetAsync(link.CreatedByUserId, targetUserId, conversation.Scope, conversation.OrganizationId, CreatorContext(conversation, link), nowUtc, ct);
            await EnsurePersonalAudienceSafetyAsync(conversation, targetUserId, nowUtc, ct);
            await EnsureNotBannedAsync(conversation.Id, targetUserId, nowUtc, ct);
        }
        catch (MessagingException) { throw LinkUnavailable(); }
    }

    private async Task AddLinkMemberAsync(Conversation conversation, ConversationInviteLink link, Guid targetId, Guid actorId, Guid sessionId,
        string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        link.Consume(nowUtc);
        var participant = ConversationParticipant.CreateActive(conversation.Id, targetId, nowUtc, ConversationParticipantRole.Member,
            conversation.Type == ConversationType.Group && conversation.GroupHistoryPolicy == GroupHistoryPolicy.FromJoin ? conversation.NextMessageSequence : 1);
        dbContext.ConversationParticipants.Add(participant);
        await AuditAdvancedAsync(actorId, sessionId, "messaging.invite-link.joined", "ConversationInviteLink", link.Id.ToString("D"), "پذیرش عضویت با لینک", correlationId, nowUtc, ct);
        await SaveManagementAsync(ct);
        await AppendChangeAsync(conversation, MessagingChangeType.ParticipantChanged, participant.Id, VersionOf(participant.RowVersion), nowUtc, actorId, null, ct);
    }

    private async Task<DateTime> EnsureAdvancedSessionAsync(Guid userId, Guid sessionId, AccessContext context, CancellationToken ct)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        if (!await (from session in dbContext.UserSessions join user in dbContext.Users on session.UserId equals user.Id
            where session.Id == sessionId && session.UserId == userId && session.RevokedAtUtc == null && session.ExpiresAtUtc > nowUtc &&
                session.AbsoluteExpiresAtUtc > nowUtc && user.AccountStatus == UserAccountStatus.Active select session.Id).AnyAsync(ct))
            throw new IdentityException(IdentityErrorCodes.AuthRequired, "نشست معتبر لازم است.");
        if (context.WorkspaceType is not WorkspaceType.Personal and not WorkspaceType.Organization ||
            !await HasActiveActorContextAsync(userId, context.WorkspaceType == WorkspaceType.Personal ? ConversationScope.Personal : ConversationScope.Organization,
                context.OrganizationId, context, ct))
            throw new MessagingException(MessagingErrorCodes.OrganizationScopeMismatch, "نقش یا زمینهٔ جاری دیگر معتبر نیست.");
        return nowUtc;
    }

    // این context فقط selector سروری برای facts جاری است؛ احراز هویت نشست سازنده نیست و از کلاینت گرفته نمی‌شود.
    private static AccessContext CreatorContext(Conversation c, ConversationInviteLink link) =>
        new(link.CreatedByUserId, link.CreatedByUserId, Guid.Empty,
            c.Scope == ConversationScope.Personal ? WorkspaceType.Personal : WorkspaceType.Organization,
            c.OrganizationId, link.CreatorMembershipId, link.CreatorRole, null);
    private static string HashInviteToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static InviteLinkResult MapLink(ConversationInviteLink l) => new(l.Id, l.ConversationId, l.CreatedByUserId,
        AdvancedUtc(l.CreatedAtUtc), AdvancedUtc(l.ExpiresAtUtc), l.MaximumUses, l.UsedCount, l.RequiresApproval,
        l.RevokedAtUtc is null ? null : AdvancedUtc(l.RevokedAtUtc.Value), Convert.ToBase64String(l.RowVersion));
    private static ConversationJoinRequestResult MapJoinRequest(ConversationJoinRequest r, DateTime nowUtc) =>
        new(r.Id, r.ConversationId, r.UserId, r.Status == ConversationJoinRequestStatus.Pending && r.ExpiresAtUtc <= nowUtc ? ConversationJoinRequestStatus.Expired : r.Status,
            AdvancedUtc(r.CreatedAtUtc), AdvancedUtc(r.ExpiresAtUtc), Convert.ToBase64String(r.RowVersion));
}
