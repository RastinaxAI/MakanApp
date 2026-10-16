using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record SaveUserContactCommand(Guid ContactUserId, string? SavedName);

public sealed record UpdateUserContactCommand(
    string? SavedName,
    bool IsFavorite,
    string ExpectedVersion);

public sealed record UpdateMessagingPrivacyCommand(
    PrivacyAudience DirectMessagesAudience,
    PrivacyAudience PhoneNumberVisibilityAudience,
    PrivacyAudience PhoneNumberDiscoveryAudience,
    PrivacyAudience GroupInvitesAudience,
    string? ExpectedVersion,
    PrivacyAudience? OnlineAudience = null,
    PrivacyAudience? LastSeenAudience = null);

public sealed record ResolvePersonCommand(string? Username, string? PhoneNumber);

public sealed record MessagingPublicProfileResult(
    Guid UserId,
    string Username,
    string DisplayName,
    string? PhoneNumber,
    bool IsSavedContact,
    bool IsFavorite,
    bool CanStartDirect);

public sealed record UserContactResult(
    Guid ContactUserId,
    string? SavedName,
    bool IsFavorite,
    MessagingPublicProfileResult Profile,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string Version,
    bool AlreadyExisted = false);

public sealed record UserContactPageResult(
    IReadOnlyCollection<UserContactResult> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record RecentContactResult(
    MessagingPublicProfileResult Profile,
    DateTime LastActivityAtUtc);

public sealed record RecentContactPageResult(
    IReadOnlyCollection<RecentContactResult> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record PeopleSearchPageResult(
    IReadOnlyCollection<MessagingPublicProfileResult> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record PeopleResolveResult(
    bool IsDiscoverable,
    MessagingPublicProfileResult? Profile);

public sealed record MessagingPrivacySettingsResult(
    PrivacyAudience DirectMessagesAudience,
    PrivacyAudience PhoneNumberVisibilityAudience,
    PrivacyAudience PhoneNumberDiscoveryAudience,
    PrivacyAudience GroupInvitesAudience,
    bool IsExplicit,
    string? Version,
    PrivacyAudience OnlineAudience = MessagingPrivacySettings.DefaultOnlineAudience,
    PrivacyAudience LastSeenAudience = MessagingPrivacySettings.DefaultLastSeenAudience);
