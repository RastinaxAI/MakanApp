using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private async Task EnsureContactCardAudienceAsync(Conversation conversation, Guid actorId, SendMessageStoreCommand command, CancellationToken ct)
    {
        if (command.Kind != MessageKind.ContactCard) return;
        if (!command.ContactUserId.HasValue) throw RecipientNotAvailable();
        var subjectId = command.ContactUserId.Value; var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!await MessengerRelationshipQuery.DiscoverableAccounts(dbContext, actorId, now)
            .AnyAsync(u => u.Id == subjectId && u.ProfileCompletedAtUtc != null && u.PersonId != null && u.Username != null, ct))
            throw RecipientNotAvailable();
        var official = OfficialSpaceAccessQuery.Conversations(dbContext);
        // عضویت کلاس مرجع است؛ participant تازه ممکن است هنوز در همان transaction ذخیره نشده باشد.
        var audienceIds = conversation.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic
            ? official.Where(m => m.ConversationId == conversation.Id).Select(m => m.UserId)
            : dbContext.ConversationParticipants.Where(p => p.ConversationId == conversation.Id && p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null).Select(p => p.UserId);
        // یک query برای همهٔ مخاطبان بالقوهٔ مجاز؛ نه یک profile lookup به‌ازای هر عضو.
        var viewers = from u in dbContext.Users
            where audienceIds.Contains(u.Id) &&
                u.AccountStatus == UserAccountStatus.Active && u.ProfileCompletedAtUtc != null && u.PersonId != null &&
                (conversation.Type != ConversationType.Saved || conversation.SavedOwnerUserId == u.Id) &&
                (conversation.Scope == ConversationScope.Personal || dbContext.Memberships.Any(m => m.UserId == u.Id && m.OrganizationId == conversation.OrganizationId &&
                    m.Status == MembershipStatus.Active && m.EndedAtUtc == null && dbContext.RoleAssignments.Any(r => r.MembershipId == m.Id && r.Status == RoleAssignmentStatus.Active && r.EndedAtUtc == null)))
            select u;
        // کارت فقط username/display name دارد؛ شماره را حمل نمی‌کند و Block در هر دو جهت محترم است.
        if (await viewers.AnyAsync(u => dbContext.UserBlocks.Any(b => b.Status == UserBlockStatus.Active && b.EndedAtUtc == null &&
            (b.BlockerUserId == subjectId && b.BlockedUserId == u.Id ||
             b.BlockerUserId == u.Id && b.BlockedUserId == subjectId)), ct)) throw RecipientNotAvailable();
    }
}
