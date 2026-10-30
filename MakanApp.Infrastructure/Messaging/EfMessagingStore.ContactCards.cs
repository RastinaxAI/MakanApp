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
        var subject = await MessengerRelationshipQuery.DiscoverableAccounts(dbContext, actorId, now)
            .Where(u => u.Id == subjectId && u.ProfileCompletedAtUtc != null && u.PersonId != null && u.Username != null)
            .Select(u => new { IsAdult = u.CommunicationAgeCategory == CommunicationAgeCategory.Adult && u.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified })
            .SingleOrDefaultAsync(ct) ?? throw RecipientNotAvailable();
        var related = MessengerRelationshipQuery.RelatedUsers(dbContext, subjectId, now);
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
        if (await viewers.AnyAsync(u => u.Id != subjectId &&
            !(subject.IsAdult && u.CommunicationAgeCategory == CommunicationAgeCategory.Adult && u.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified) &&
            !related.Contains(u.Id), ct)) throw RecipientNotAvailable();
    }
}
