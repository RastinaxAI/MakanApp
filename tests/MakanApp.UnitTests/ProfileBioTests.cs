using MakanApp.Domain.Identity;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class ProfileBioTests
{
    [Fact]
    public void BioIsOptionalTrimmedAndCanBeClearedWithoutChangingIdentity()
    {
        var user = User.Create(DateTime.UtcNow); var id = user.Id;
        Assert.Null(user.Bio);
        user.SetBio("  معرفی من\nخط دوم  ", DateTime.UtcNow);
        Assert.Equal("معرفی من\nخط دوم", user.Bio);
        user.SetBio("  ", DateTime.UtcNow);
        Assert.Null(user.Bio); Assert.Equal(id, user.Id);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void BioHasDocumentedTechnicalSizeSafeguard(int length, bool accepted)
    {
        var user = User.Create(DateTime.UtcNow);
        if (accepted) { user.SetBio(new string('a', length), DateTime.UtcNow); Assert.Equal(length, user.Bio!.Length); }
        else Assert.Throws<ArgumentException>(() => user.SetBio(new string('a', length), DateTime.UtcNow));
    }

    [Fact]
    public void BioRejectsHiddenControlCharacters()
    {
        var user = User.Create(DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => user.SetBio("a\0b", DateTime.UtcNow));
        Assert.Null(user.Bio);
    }
}
