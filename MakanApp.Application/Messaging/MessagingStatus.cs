using MakanApp.Domain.Identity;

namespace MakanApp.Application.Messaging;

public enum PersonalMessagingAction
{
    ReadExistingConversations = 1,
    DiscoverUsers = 2,
    CreateDirect = 3,
    CreatePersonalGroup = 4,
    CreatePersonalChannel = 5,
    DiscoverRelatedUsers = 6,
    CreateDirectWithAuthorizedRelationship = 7,
    CreatePrivatePersonalGroup = 8
}

public enum MessagingNextAction
{
    None = 0,
    VerificationRequired = 1,
    ProfileCompletionRequired = 2,
    VerificationConflictResolutionRequired = 3,
    MessagingRestrictionActive = 4,
    SelectPersonalWorkspace = 5
}

public sealed record MessagingStatusSnapshot(
    CommunicationAgeVerificationStatus VerificationStatus,
    CommunicationAgeCategory AgeCategory,
    bool ProfileComplete,
    bool PersonalWorkspaceSelected,
    DateTime? MessagingRestrictedUntilUtc,
    string Version);

public sealed record MessagingStatusResult(
    CommunicationAgeVerificationStatus VerificationStatus,
    CommunicationAgeCategory EffectiveAgeCategory,
    IReadOnlyCollection<PersonalMessagingAction> AllowedActions,
    MessagingNextAction NextAction,
    string Version);

public static class MessagingStatusPolicy
{
    public const string PolicyVersion = "messaging-20261101";

    public static MessagingStatusResult Evaluate(MessagingStatusSnapshot state, DateTime nowUtc)
    {
        var verified = state.VerificationStatus == CommunicationAgeVerificationStatus.Verified;
        var effectiveAge = verified && Enum.IsDefined(state.AgeCategory) ? state.AgeCategory : CommunicationAgeCategory.Unknown;
        // پروفایل ناقص مجوز تازه‌ای نمی‌سازد؛ دسترسی خواندن همچنان برای هر گفتگو بررسی می‌شود.
        var actions = new List<PersonalMessagingAction>
        {
            PersonalMessagingAction.ReadExistingConversations
        };
        // تأیید سن واقعیت پروفایل است؛ سایر مجوزها همچنان در منبع بررسی می‌شوند.
        var nextAction = MessagingNextAction.None;
        var version = $"{PolicyVersion}:{state.Version}";
        if (!state.ProfileComplete)
            return new(state.VerificationStatus, effectiveAge, actions, MessagingNextAction.ProfileCompletionRequired, version);

        // این فهرست راهنمای self است؛ مجوز مقصد/منبع در هر عملیات دوباره محاسبه می‌شود.
        actions.Add(PersonalMessagingAction.DiscoverUsers);
        // عملیات صریح Personal از قبل مستقل از Workspace انتخاب‌شده اجرا می‌شود؛ این فهرست مجوز سازمانی نمی‌دهد.
        if (state.MessagingRestrictedUntilUtc is null || state.MessagingRestrictedUntilUtc <= nowUtc)
        {
            actions.Add(PersonalMessagingAction.CreatePersonalGroup);
            actions.Add(PersonalMessagingAction.CreatePersonalChannel);
            actions.Add(PersonalMessagingAction.CreateDirect);
        }
        if (state.MessagingRestrictedUntilUtc > nowUtc)
            nextAction = MessagingNextAction.MessagingRestrictionActive;
        return new(state.VerificationStatus, effectiveAge, actions, nextAction, version);
    }
}
