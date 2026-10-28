using MakanApp.Domain.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class PersonDateOfBirthTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 0, 15, 0, DateTimeKind.Utc);

    [Fact]
    public void ExistingProfileDoesNotRequireDateOfBirth()
    {
        var person = Person.Create("نام", "نام خانوادگی", null, Now);
        Assert.Null(person.DateOfBirth);
    }

    [Theory]
    [InlineData(2005, 8, 21)]
    [InlineData(2026, 10, 28)]
    [InlineData(1800, 1, 1)]
    [InlineData(2000, 2, 29)]
    public void SelfDeclaredDateHasNoInventedMinimumAge(int year, int month, int day)
    {
        var person = Person.Create("نام", "نام خانوادگی", null, Now);
        var date = new DateOnly(year, month, day);
        person.SetDateOfBirth(date, Now);
        Assert.Equal(date, person.DateOfBirth);
        Assert.Equal(Now, person.UpdatedAtUtc);
    }

    [Fact]
    public void FutureDateDoesNotMutatePerson()
    {
        var person = Person.Create("نام", "نام خانوادگی", null, Now);
        Assert.Throws<ArgumentOutOfRangeException>(() => person.SetDateOfBirth(new DateOnly(2026, 10, 29), Now));
        Assert.Null(person.DateOfBirth);
        Assert.Null(person.UpdatedAtUtc);
    }

    [Fact]
    public void UpdatingNamesPreservesDeclaredDate()
    {
        var person = Person.Create("نام", "نام خانوادگی", null, Now);
        person.SetDateOfBirth(new DateOnly(2005, 8, 21), Now);
        person.Update("نام جدید", "نام خانوادگی", null, Now.AddMinutes(1));
        Assert.Equal(new DateOnly(2005, 8, 21), person.DateOfBirth);
    }
}
