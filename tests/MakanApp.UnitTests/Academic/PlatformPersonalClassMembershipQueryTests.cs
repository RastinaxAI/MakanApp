using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using Xunit;

namespace MakanApp.UnitTests.Academic;

public sealed class PlatformPersonalClassMembershipQueryTests
{
    [Theory]
    [InlineData(0, 30)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void AdminRosterRejectsUnboundedOrInvalidPages(int page, int size) =>
        Assert.Throws<AcademicException>(() => PersonalClassAdminMembershipService.Normalize(new(Page: page, PageSize: size)));

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void AdminRosterRejectsUnknownRoleBits(int value) =>
        Assert.Throws<AcademicException>(() => PersonalClassAdminMembershipService.Normalize(new(Role: (PersonalClassLocalRoles)value)));

    [Fact]
    public void AdminRosterNormalizesSearchWithoutChangingMembershipSelection()
    {
        var result = PersonalClassAdminMembershipService.Normalize(new("  Teacher  ", PersonalClassLocalRoles.Teacher, true));
        Assert.Equal("Teacher", result.Search); Assert.True(result.ActiveOnly); Assert.Equal(PersonalClassLocalRoles.Teacher, result.Role);
        Assert.Null(PersonalClassAdminMembershipService.Normalize(new(Search: " ")).Search);
        Assert.Throws<AcademicException>(() => PersonalClassAdminMembershipService.Normalize(new(Search: new string('x', 101))));
    }
}
