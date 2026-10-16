using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record EffectiveMessagingPrivacySettings(
    PrivacyAudience DirectMessagesAudience,
    PrivacyAudience PhoneNumberVisibilityAudience,
    PrivacyAudience PhoneNumberDiscoveryAudience,
    PrivacyAudience GroupInvitesAudience,
    bool IsExplicit,
    byte[]? Version,
    PrivacyAudience OnlineAudience = MessagingPrivacySettings.DefaultOnlineAudience,
    PrivacyAudience LastSeenAudience = MessagingPrivacySettings.DefaultLastSeenAudience);

public sealed record MessagingPrivacyContext(
    EffectiveMessagingPrivacySettings Settings,
    bool TargetHasSavedRequester);

public interface IMessagingPrivacyResolver
{
    Task<MessagingPrivacyContext> ResolveAsync(
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken);
}

public static class MessagingPrivacyPolicy
{
    public static bool Allows(
        PrivacyAudience audience,
        bool targetHasSavedRequester,
        bool requesterIsTarget = false) =>
        requesterIsTarget || audience switch
        {
            PrivacyAudience.Everyone => true,
            PrivacyAudience.Contacts => targetHasSavedRequester,
            PrivacyAudience.Nobody => false,
            _ => false
        };

    public static EffectiveMessagingPrivacySettings Defaults() => new(
        MessagingPrivacySettings.DefaultDirectMessagesAudience,
        MessagingPrivacySettings.DefaultPhoneNumberVisibilityAudience,
        MessagingPrivacySettings.DefaultPhoneNumberDiscoveryAudience,
        MessagingPrivacySettings.DefaultGroupInvitesAudience,
        false,
        null);
}
