using MakanApp.Application.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed class EfMessagingPrivacyResolver(MakanDbContext dbContext)
    : IMessagingPrivacyResolver
{
    public async Task<MessagingPrivacyContext> ResolveAsync(
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.MessagingPrivacySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == targetUserId, cancellationToken);
        var effective = settings is null
            ? MessagingPrivacyPolicy.Defaults()
            : new EffectiveMessagingPrivacySettings(
                settings.DirectMessagesAudience,
                settings.PhoneNumberVisibilityAudience,
                settings.PhoneNumberDiscoveryAudience,
                settings.GroupInvitesAudience,
                true,
                settings.RowVersion,
                settings.OnlineAudience,
                settings.LastSeenAudience);
        var targetHasSavedRequester = requesterUserId == targetUserId ||
            await dbContext.UserContacts.AsNoTracking().AnyAsync(
                contact => contact.OwnerUserId == targetUserId &&
                           contact.ContactUserId == requesterUserId,
                cancellationToken);
        return new MessagingPrivacyContext(effective, targetHasSavedRequester);
    }
}
