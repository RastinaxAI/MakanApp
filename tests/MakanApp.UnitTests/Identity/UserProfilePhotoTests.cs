using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class UserProfilePhotoTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewUserHasNoPhotoAndPhotoDoesNotCompleteIdentity()
    {
        var user = User.Create(Now);
        Assert.Null(user.ProfilePhotoFileAssetId);
        user.SetProfilePhoto(Guid.NewGuid(), Now);
        Assert.False(user.IsProfileComplete);
        Assert.Null(user.PersonId);
    }

    [Fact]
    public void ReplaceAndRemoveChangeOnlyCurrentPhotoReference()
    {
        var user = User.Create(Now);
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        user.SetProfilePhoto(first, Now);
        user.SetProfilePhoto(second, Now.AddMinutes(1));
        Assert.Equal(second, user.ProfilePhotoFileAssetId);
        user.SetProfilePhoto(null, Now.AddMinutes(2));
        Assert.Null(user.ProfilePhotoFileAssetId);
        Assert.Equal(UserAccountStatus.Active, user.AccountStatus);
    }

    [Fact]
    public void EmptyIdentifierIsRejected() =>
        Assert.Throws<ArgumentException>(() => User.Create(Now).SetProfilePhoto(Guid.Empty, Now));

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void PhotoChangesRequireUtc(DateTimeKind kind) =>
        Assert.Throws<ArgumentException>(() => User.Create(Now).SetProfilePhoto(Guid.NewGuid(), DateTime.SpecifyKind(Now, kind)));

    [Fact]
    public void RepeatingTheSameSelectionIsIdempotent()
    {
        var user = User.Create(Now); var id = Guid.NewGuid();
        user.SetProfilePhoto(id, Now);
        user.SetProfilePhoto(id, Now.AddHours(1));
        Assert.Equal(Now, user.UpdatedAtUtc);
    }

    [Fact]
    public void SuspensionAndAgeChangesDoNotDeletePhoto()
    {
        var user = User.Create(Now); var id = Guid.NewGuid();
        user.SetProfilePhoto(id, Now);
        user.SetCommunicationAgeCategory(CommunicationAgeCategory.Minor, Now);
        user.Suspend(Now);
        Assert.Equal(id, user.ProfilePhotoFileAssetId);
        user.Reactivate(Now.AddMinutes(1));
        Assert.Equal(id, user.ProfilePhotoFileAssetId);
        Assert.Equal(CommunicationAgeCategory.Minor, user.CommunicationAgeCategory);
    }

    [Fact]
    public void CompletingPersonalProfilePreservesPhoto()
    {
        var user = User.Create(Now); var id = Guid.NewGuid();
        user.SetProfilePhoto(id, Now);
        user.SetProfile(Guid.NewGuid(), "personal.user", "PERSONAL.USER", Now);
        Assert.Equal(id, user.ProfilePhotoFileAssetId);
        Assert.True(user.IsProfileComplete);
    }

    [Fact]
    public void DescriptorUsesFileIdentityWithoutStorageLocationOrCredential()
    {
        var id = Guid.NewGuid(); var result = new ProfilePhotoResult(id, "image/png");
        Assert.Equal($"/api/v1/files/{id:D}/content", result.DownloadUrl);
        Assert.DoesNotContain("?", result.DownloadUrl);
        Assert.False(Uri.TryCreate(result.DownloadUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https");
    }
}
