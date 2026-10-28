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
    public static MessagingStatusResult Evaluate(MessagingStatusSnapshot state, DateTime nowUtc)
    {
        var verified = state.VerificationStatus == CommunicationAgeVerificationStatus.Verified;
        var effectiveAge = verified && Enum.IsDefined(state.AgeCategory) ? state.AgeCategory : CommunicationAgeCategory.Unknown;
        var adult = effectiveAge == CommunicationAgeCategory.Adult;
        // پروفایل ناقص مجوز تازه‌ای نمی‌سازد؛ دسترسی خواندن همچنان برای هر گفتگو بررسی می‌شود.
        var actions = new List<PersonalMessagingAction>
        {
            PersonalMessagingAction.ReadExistingConversations
        };
        var nextAction = state.VerificationStatus == CommunicationAgeVerificationStatus.Conflict
            ? MessagingNextAction.VerificationConflictResolutionRequired
            : !verified ? MessagingNextAction.VerificationRequired
            : MessagingNextAction.None;
        if (!state.ProfileComplete)
            return new(state.VerificationStatus, effectiveAge, actions, MessagingNextAction.ProfileCompletionRequired, state.Version);

        // این فهرست راهنمای self است؛ مجوز مقصد/منبع در هر عملیات دوباره محاسبه می‌شود.
        actions.Add(adult ? PersonalMessagingAction.DiscoverUsers : PersonalMessagingAction.DiscoverRelatedUsers);
        if (state.PersonalWorkspaceSelected)
        {
            actions.Add(adult ? PersonalMessagingAction.CreatePersonalGroup : PersonalMessagingAction.CreatePrivatePersonalGroup);
            if (adult) actions.Add(PersonalMessagingAction.CreatePersonalChannel);
            if (state.MessagingRestrictedUntilUtc is null || state.MessagingRestrictedUntilUtc <= nowUtc)
                actions.Add(adult ? PersonalMessagingAction.CreateDirect : PersonalMessagingAction.CreateDirectWithAuthorizedRelationship);
        }
        else if (nextAction == MessagingNextAction.None)
            nextAction = MessagingNextAction.SelectPersonalWorkspace;
        if (state.MessagingRestrictedUntilUtc > nowUtc)
            nextAction = MessagingNextAction.MessagingRestrictionActive;
        return new(state.VerificationStatus, effectiveAge, actions, nextAction, state.Version);
    }
}
