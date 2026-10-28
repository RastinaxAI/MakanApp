using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class PersonalMessagingStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 8, 0, 0, DateTimeKind.Utc);
    private static MessagingStatusSnapshot Adult => new(CommunicationAgeVerificationStatus.Verified,
        CommunicationAgeCategory.Adult, true, true, null, "version");

    [Fact]
    public void VerifiedAdultHasPersonalCapabilitiesWithoutAnySchoolRole()
    {
        var result = MessagingStatusPolicy.Evaluate(Adult, Now);
        Assert.Equal(CommunicationAgeCategory.Adult, result.EffectiveAgeCategory);
        Assert.Equal(MessagingNextAction.None, result.NextAction);
        Assert.Contains(PersonalMessagingAction.DiscoverUsers, result.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreateDirect, result.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreatePersonalGroup, result.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreatePersonalChannel, result.AllowedActions);
    }

    [Theory]
    [InlineData(CommunicationAgeVerificationStatus.Unverified, CommunicationAgeCategory.Adult)]
    [InlineData(CommunicationAgeVerificationStatus.Conflict, CommunicationAgeCategory.Adult)]
    [InlineData(CommunicationAgeVerificationStatus.Verified, CommunicationAgeCategory.Minor)]
    [InlineData(CommunicationAgeVerificationStatus.Verified, CommunicationAgeCategory.Unknown)]
    public void ProtectedStateNeverAdvertisesGlobalStrangerDiscoveryOrPersonalChannel(
        CommunicationAgeVerificationStatus status, CommunicationAgeCategory category)
    {
        var result = MessagingStatusPolicy.Evaluate(Adult with { VerificationStatus = status, AgeCategory = category }, Now);
        Assert.NotEqual(CommunicationAgeCategory.Adult, result.EffectiveAgeCategory);
        Assert.DoesNotContain(PersonalMessagingAction.DiscoverUsers, result.AllowedActions);
        Assert.DoesNotContain(PersonalMessagingAction.CreateDirect, result.AllowedActions);
        Assert.DoesNotContain(PersonalMessagingAction.CreatePersonalChannel, result.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreatePrivatePersonalGroup, result.AllowedActions);
        Assert.Contains(PersonalMessagingAction.CreateDirectWithAuthorizedRelationship, result.AllowedActions);
        Assert.Equal(status == CommunicationAgeVerificationStatus.Conflict
            ? MessagingNextAction.VerificationConflictResolutionRequired
            : status == CommunicationAgeVerificationStatus.Unverified ? MessagingNextAction.VerificationRequired : MessagingNextAction.None,
            result.NextAction);
    }

    [Fact]
    public void ProfileCompletionIsNotAgeVerificationAndIncompleteProfileKeepsReadOnlyAccess()
    {
        var result = MessagingStatusPolicy.Evaluate(Adult with { ProfileComplete = false, VerificationStatus = CommunicationAgeVerificationStatus.Unverified }, Now);
        Assert.Equal(PersonalMessagingAction.ReadExistingConversations, Assert.Single(result.AllowedActions));
        Assert.Equal(MessagingNextAction.ProfileCompletionRequired, result.NextAction);
        Assert.Equal(CommunicationAgeCategory.Unknown, result.EffectiveAgeCategory);
    }

    [Theory]
    [InlineData(CommunicationAgeVerificationStatus.Unverified, CommunicationAgeCategory.Adult, true)]
    [InlineData(CommunicationAgeVerificationStatus.Conflict, CommunicationAgeCategory.Adult, false)]
    [InlineData(CommunicationAgeVerificationStatus.Verified, CommunicationAgeCategory.Minor, true)]
    [InlineData(CommunicationAgeVerificationStatus.Verified, CommunicationAgeCategory.Adult, false)]
    public void IncompleteProfileNeverGrantsCreationDiscoveryOrAgeVerification(
        CommunicationAgeVerificationStatus status, CommunicationAgeCategory age, bool personalWorkspace)
    {
        var result = MessagingStatusPolicy.Evaluate(Adult with
        {
            ProfileComplete = false,
            VerificationStatus = status,
            AgeCategory = age,
            PersonalWorkspaceSelected = personalWorkspace,
            MessagingRestrictedUntilUtc = Now.AddHours(1)
        }, Now);
        Assert.Equal(PersonalMessagingAction.ReadExistingConversations, Assert.Single(result.AllowedActions));
        Assert.Equal(MessagingNextAction.ProfileCompletionRequired, result.NextAction);
        Assert.Equal(status, result.VerificationStatus);
        Assert.Equal(status == CommunicationAgeVerificationStatus.Verified ? age : CommunicationAgeCategory.Unknown,
            result.EffectiveAgeCategory);
    }

    [Fact]
    public void OrganizationWorkspaceDoesNotAdvertisePersonalCreationOrGrantOrganizationPrivileges()
    {
        var result = MessagingStatusPolicy.Evaluate(Adult with { PersonalWorkspaceSelected = false }, Now);
        Assert.Contains(PersonalMessagingAction.ReadExistingConversations, result.AllowedActions);
        Assert.Contains(PersonalMessagingAction.DiscoverUsers, result.AllowedActions);
        Assert.DoesNotContain(PersonalMessagingAction.CreatePersonalGroup, result.AllowedActions);
        Assert.DoesNotContain(PersonalMessagingAction.CreateDirect, result.AllowedActions);
        Assert.Equal(MessagingNextAction.SelectPersonalWorkspace, result.NextAction);
    }

    [Fact]
    public void RestrictionDoesNotDeleteHistoryAndExpiredRestrictionDoesNotRemainEffective()
    {
        var restricted = MessagingStatusPolicy.Evaluate(Adult with { MessagingRestrictedUntilUtc = Now.AddMinutes(1) }, Now);
        Assert.Contains(PersonalMessagingAction.ReadExistingConversations, restricted.AllowedActions);
        Assert.DoesNotContain(PersonalMessagingAction.CreateDirect, restricted.AllowedActions);
        Assert.Equal(MessagingNextAction.MessagingRestrictionActive, restricted.NextAction);
        var expired = MessagingStatusPolicy.Evaluate(Adult with { MessagingRestrictedUntilUtc = Now }, Now);
        Assert.Contains(PersonalMessagingAction.CreateDirect, expired.AllowedActions);
        Assert.Equal(MessagingNextAction.None, expired.NextAction);
    }

    [Fact]
    public void PlatformInitialEvidenceUsesExistingAgeModelAndCannotClaimSchoolOrganization()
    {
        var evidence = CommunicationAgeVerification.Create(Guid.NewGuid(), new DateOnly(1990, 1, 1),
            CommunicationAgeCategory.Unknown, false, CommunicationAgeVerificationSource.PlatformVerification,
            Guid.NewGuid(), null, "شاهد مجاز اولیه", Now);
        Assert.Equal(CommunicationAgeCategory.Adult, evidence.ProposedCategory);
        Assert.Equal(CommunicationAgeVerificationStatus.Verified, evidence.Status);
        Assert.Null(evidence.OrganizationId);
        Assert.Throws<ArgumentException>(() => CommunicationAgeVerification.Create(Guid.NewGuid(), new DateOnly(1990, 1, 1),
            CommunicationAgeCategory.Unknown, false, CommunicationAgeVerificationSource.PlatformVerification,
            Guid.NewGuid(), Guid.NewGuid(), "مرز سازمان نامعتبر", Now));
    }
}
