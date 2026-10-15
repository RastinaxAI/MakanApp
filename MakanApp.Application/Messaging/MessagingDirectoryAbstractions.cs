using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record MessagingProfileStoreRecord(
    Guid UserId,
    string Username,
    string DisplayName,
    string? NormalizedPhoneNumber,
    PrivacyAudience DirectMessagesAudience,
    PrivacyAudience PhoneNumberVisibilityAudience,
    PrivacyAudience PhoneNumberDiscoveryAudience,
    bool TargetHasSavedRequester,
    bool IsSavedContact,
    bool IsFavorite,
    bool HasEffectiveDirectBlock,
    bool IsProtectedAccount = false,
    bool SafetyAllowsDirect = true);

public sealed record UserContactStoreRecord(
    UserContact Contact,
    MessagingProfileStoreRecord Profile);

public sealed record SaveUserContactStoreResult(
    UserContact Contact,
    MessagingProfileStoreRecord Profile,
    bool AlreadyExisted);

public sealed record UserContactStorePage(
    IReadOnlyCollection<UserContactStoreRecord> Items,
    bool HasMore);

public sealed record MessagingProfileStorePage(
    IReadOnlyCollection<MessagingProfileStoreRecord> Items,
    bool HasMore);

public sealed record RecentContactStoreRecord(
    MessagingProfileStoreRecord Profile,
    DateTime LastActivityAtUtc);

public sealed record RecentContactStorePage(
    IReadOnlyCollection<RecentContactStoreRecord> Items,
    bool HasMore);

public interface IMessagingDirectoryStore
{
    Task<UserContactStorePage> GetContactsAsync(
        Guid ownerUserId,
        string? query,
        bool favoritesOnly,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<SaveUserContactStoreResult> SaveContactAsync(
        Guid ownerUserId,
        Guid contactUserId,
        string? savedName,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<UserContactStoreRecord> UpdateContactAsync(
        Guid ownerUserId,
        Guid contactUserId,
        string? savedName,
        bool isFavorite,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task DeleteContactAsync(
        Guid ownerUserId,
        Guid contactUserId,
        CancellationToken cancellationToken);

    Task<RecentContactStorePage> GetRecentContactsAsync(
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<MessagingProfileStorePage> SearchPeopleAsync(
        Guid requesterUserId,
        string query,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<MessagingProfileStoreRecord?> FindByUsernameAsync(
        Guid requesterUserId,
        string normalizedUsername,
        CancellationToken cancellationToken);

    Task<MessagingProfileStoreRecord?> FindByPhoneAsync(
        Guid requesterUserId,
        string normalizedPhoneNumber,
        CancellationToken cancellationToken);

    Task<MessagingProfileStoreRecord?> FindByUserIdAsync(
        Guid requesterUserId,
        Guid targetUserId,
        CancellationToken cancellationToken);

    Task<MessagingPrivacySettings?> GetPrivacySettingsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<MessagingPrivacySettings> SavePrivacySettingsAsync(
        Guid userId,
        PrivacyAudience directMessagesAudience,
        PrivacyAudience phoneNumberVisibilityAudience,
        PrivacyAudience phoneNumberDiscoveryAudience,
        PrivacyAudience groupInvitesAudience,
        byte[]? expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}

public interface IMessagingDirectoryService
{
    Task<UserContactPageResult> GetContactsAsync(
        Guid userId,
        string? query,
        bool favoritesOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<UserContactResult> SaveContactAsync(
        Guid userId,
        SaveUserContactCommand command,
        CancellationToken cancellationToken);

    Task<UserContactResult> UpdateContactAsync(
        Guid userId,
        Guid contactUserId,
        UpdateUserContactCommand command,
        CancellationToken cancellationToken);

    Task DeleteContactAsync(
        Guid userId,
        Guid contactUserId,
        CancellationToken cancellationToken);

    Task<RecentContactPageResult> GetRecentContactsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PeopleSearchPageResult> SearchPeopleAsync(
        Guid userId,
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PeopleResolveResult> ResolvePersonAsync(
        Guid userId,
        ResolvePersonCommand command,
        CancellationToken cancellationToken);

    Task<MessagingPublicProfileResult> GetPublicProfileAsync(
        Guid userId,
        Guid targetUserId,
        CancellationToken cancellationToken);

    Task<MessagingPrivacySettingsResult> GetPrivacySettingsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<MessagingPrivacySettingsResult> UpdatePrivacySettingsAsync(
        Guid userId,
        UpdateMessagingPrivacyCommand command,
        CancellationToken cancellationToken);
}
