using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private async Task EnsureActorCanMessageAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        if (!await dbContext.Users.AnyAsync(u => u.Id == userId && u.AccountStatus == UserAccountStatus.Active &&
            (u.MessagingRestrictedUntilUtc == null || u.MessagingRestrictedUntilUtc <= nowUtc), ct))
            throw new MessagingException(MessagingErrorCodes.MessageNotAllowed, "ارسال پیام در وضعیت جاری مجاز نیست.");
    }

    private async Task EnsurePersonalAudienceSafetyAsync(Conversation conversation, Guid actorId, DateTime nowUtc, CancellationToken ct)
    {
        if (conversation.Scope != ConversationScope.Personal || conversation.Type == ConversationType.Saved) return;
        var actorAdult = await IsVerifiedAdultAsync(actorId, ct);
        var related = MessengerRelationshipQuery.RelatedUsers(dbContext, actorId, nowUtc);
        var unsafeAudience = await (from p in dbContext.ConversationParticipants
            join user in dbContext.Users on p.UserId equals user.Id
            where p.ConversationId == conversation.Id && p.UserId != actorId &&
                p.Status == ConversationParticipantStatus.Active && p.EndedAtUtc == null &&
                (!actorAdult || user.CommunicationAgeCategory != CommunicationAgeCategory.Adult ||
                 user.CommunicationAgeVerificationStatus != CommunicationAgeVerificationStatus.Verified) &&
                !related.Contains(user.Id)
            select p.Id).AnyAsync(ct);
        if (unsafeAudience) throw new MessagingException(MessagingErrorCodes.MessageNotAllowed, "رابطه ارتباطی جاری مجاز نیست.");
    }

    private Task<bool> IsVerifiedAdultAsync(Guid userId, CancellationToken ct) => dbContext.Users.AnyAsync(u => u.Id == userId &&
        u.AccountStatus == UserAccountStatus.Active && u.CommunicationAgeCategory == CommunicationAgeCategory.Adult &&
        u.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified, ct);

    private async Task EnsurePersonalCreationSafetyAsync(Guid actorId, ConversationScope scope, ConversationType type,
        ConversationVisibility visibility, CancellationToken ct)
    {
        if (scope == ConversationScope.Personal && (type == ConversationType.Channel || visibility == ConversationVisibility.Public) &&
            !await IsVerifiedAdultAsync(actorId, ct)) throw ManagementNotAllowed();
    }

    private async Task EnsureInstantAddIsSafeAsync(Guid actorId, Guid targetId, ConversationScope scope, CancellationToken ct)
    {
        if (scope == ConversationScope.Personal &&
            (!await IsVerifiedAdultAsync(actorId, ct) || !await IsVerifiedAdultAsync(targetId, ct)))
            throw new MessagingException("CONVERSATION_INVITATION_REQUIRED", "دعوت و پذیرش صریح عضو لازم است.");
    }
}
