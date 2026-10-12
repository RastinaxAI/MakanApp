using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingDirectoryModelTests
{
    [Fact]
    public void UserContactRejectsSelfAndNormalizesPrivateSavedName()
    {
        var ownerUserId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => UserContact.Create(
            ownerUserId,
            ownerUserId,
            null,
            DateTime.UtcNow));

        var contact = UserContact.Create(
            ownerUserId,
            Guid.NewGuid(),
            "  هم‌کلاسی زبان  ",
            DateTime.UtcNow);
        Assert.Equal("هم‌کلاسی زبان", contact.SavedName);
    }

    [Fact]
    public void UserContactRejectsSavedNameBeyondStorageLimit()
    {
        Assert.Throws<ArgumentException>(() => UserContact.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('ن', UserContact.MaximumSavedNameLength + 1),
            DateTime.UtcNow));
    }

    [Fact]
    public void MessagingPrivacyDefaultsAreExplicitProductDefaultsWithoutRow()
    {
        var defaults = MessagingPrivacyPolicy.Defaults();

        Assert.Equal(PrivacyAudience.Everyone, defaults.DirectMessagesAudience);
        Assert.Equal(PrivacyAudience.Nobody, defaults.PhoneNumberVisibilityAudience);
        Assert.Equal(PrivacyAudience.Nobody, defaults.PhoneNumberDiscoveryAudience);
        Assert.Equal(PrivacyAudience.Contacts, defaults.GroupInvitesAudience);
        Assert.False(defaults.IsExplicit);
        Assert.Null(defaults.Version);
    }

    [Theory]
    [InlineData(PrivacyAudience.Everyone, false, true)]
    [InlineData(PrivacyAudience.Contacts, true, true)]
    [InlineData(PrivacyAudience.Contacts, false, false)]
    [InlineData(PrivacyAudience.Nobody, true, false)]
    public void PrivacyAudienceUsesTargetOwnedOneWayContact(
        PrivacyAudience audience,
        bool targetHasSavedRequester,
        bool expected)
    {
        Assert.Equal(
            expected,
            MessagingPrivacyPolicy.Allows(audience, targetHasSavedRequester));
    }

    [Fact]
    public void MessagingPrivacySettingsRejectInvalidAudience()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MessagingPrivacySettings.Create(
            Guid.NewGuid(),
            (PrivacyAudience)99,
            PrivacyAudience.Nobody,
            PrivacyAudience.Nobody,
            PrivacyAudience.Contacts,
            DateTime.UtcNow));
    }
}
