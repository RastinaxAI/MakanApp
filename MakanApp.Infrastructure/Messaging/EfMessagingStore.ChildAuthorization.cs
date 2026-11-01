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

    private static Task EnsureInstantAddIsSafeAsync(Guid actorId, Guid targetId, ConversationScope scope, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // رضایت عضویت شخصی برای تمام حساب‌ها یکسان است و از سن نتیجه نمی‌شود.
        if (scope == ConversationScope.Personal)
            throw new MessagingException("CONVERSATION_INVITATION_REQUIRED", "دعوت و پذیرش صریح عضو لازم است.");
        return Task.CompletedTask;
    }
}
