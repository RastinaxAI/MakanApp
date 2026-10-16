using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ConversationJoinRequestPageResult> GetJoinRequestsAsync(Guid actorId, Guid? conversationId, AccessContext context,
        int skip, int take, CancellationToken ct)
    {
        if (conversationId.HasValue) await LoadAdvancedManagerAsync(actorId, conversationId.Value, context, ConversationAdminPermission.ManageInvites, ct);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var rows = await dbContext.ConversationJoinRequests.AsNoTracking()
            .Where(r => conversationId.HasValue ? r.ConversationId == conversationId : r.UserId == actorId)
            .OrderByDescending(r => r.CreatedAtUtc).ThenBy(r => r.Id).Skip(skip).Take(take + 1).ToArrayAsync(ct);
        return new(rows.Take(take).Select(r => MapJoinRequest(r, nowUtc)).ToArray(), rows.Length > take);
    }

    public async Task<ConversationJoinRequestResult> CompleteJoinRequestAsync(Guid actorId, Guid sessionId, Guid requestId, ConversationJoinRequestStatus status,
        byte[] expectedVersion, AccessContext context, string correlationId, DateTime nowUtc, CancellationToken ct)
    {
        var identity = await dbContext.ConversationJoinRequests.AsNoTracking().Where(r => r.Id == requestId)
            .Select(r => new { r.ConversationId, r.UserId }).SingleOrDefaultAsync(ct) ?? throw ConversationNotFound();
        if (status == ConversationJoinRequestStatus.Cancelled && identity.UserId != actorId) throw ConversationNotFound();
        await using var transaction = await BeginManagementTransactionAsync(ct);
        Conversation conversation;
        if (status == ConversationJoinRequestStatus.Cancelled)
            conversation = await LockConversationAsync(identity.ConversationId, ct) ?? throw ConversationNotFound();
        else
        {
            var managed = await LoadManagedForMutationAsync(actorId, identity.ConversationId, context, ct);
            RequireAdvancedPermission(managed.Actor, ConversationAdminPermission.ManageInvites); conversation = managed.Conversation;
        }
        nowUtc = await EnsureAdvancedSessionAsync(actorId, sessionId, context, ct);
        var request = await dbContext.ConversationJoinRequests.SingleAsync(r => r.Id == requestId, ct);
        if (request.Status == status) { await transaction.CommitAsync(ct); return MapJoinRequest(request, nowUtc); }
        if (request.Status == ConversationJoinRequestStatus.Pending && request.ExpiresAtUtc <= nowUtc) throw LinkUnavailable();
        if (request.Status != ConversationJoinRequestStatus.Pending || !request.RowVersion.SequenceEqual(expectedVersion)) throw StateConflict();
        dbContext.Entry(request).Property(r => r.RowVersion).OriginalValue = expectedVersion;
        var memberAdded = false;
        if (status == ConversationJoinRequestStatus.Approved)
        {
            if (request.ExpiresAtUtc <= nowUtc) throw LinkUnavailable();
            var link = await dbContext.ConversationInviteLinks.SingleAsync(l => l.Id == request.InviteLinkId, ct);
            var targetContext = new AccessContext(request.UserId, request.UserId, Guid.Empty,
                conversation.Scope == ConversationScope.Personal ? WorkspaceType.Personal : WorkspaceType.Organization,
                conversation.OrganizationId, request.RequesterMembershipId, request.RequesterRole, null);
            await EnsureLinkCurrentAsync(conversation, link, request.UserId, targetContext, nowUtc, ct);
            await EnsureLinkTargetEligibleAsync(conversation, link, request.UserId, nowUtc, ct);
            if (await LoadActiveParticipantAsync(conversation.Id, request.UserId, ct) is null)
            {
                if (link.UsedCount >= link.MaximumUses) throw LinkUnavailable();
                await AddLinkMemberAsync(conversation, link, request.UserId, actorId, sessionId, correlationId, nowUtc, ct); memberAdded = true;
            }
        }
        request.Complete(status, nowUtc);
        await AuditAdvancedAsync(actorId, sessionId, "messaging.join-request." + status.ToString().ToLowerInvariant(), "ConversationJoinRequest",
            request.Id.ToString("D"), "تصمیم روی درخواست عضویت", correlationId, nowUtc, ct);
        await SaveManagementAsync(ct); await transaction.CommitAsync(ct); if (memberAdded) outboxWakeSignal.Signal();
        return MapJoinRequest(request, nowUtc);
    }
}
