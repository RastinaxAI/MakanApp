using MakanApp.Domain.Academic;
using Xunit;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.UnitTests.Academic;

public sealed class OrganizationClassSettingsTests
{
    [Theory]
    [InlineData("", 10)]
    [InlineData("title", 0)]
    public void ClassSettingsRejectInvalidTitleOrCapacity(string title, int capacity) =>
        Assert.Throws<ArgumentException>(() => NewClass().UpdateSettings(title, capacity, 0));
    [Fact]
    public void CapacityDecreaseCannotRemoveActiveLearners()
    {
        var c = NewClass(); Assert.Throws<InvalidOperationException>(() => c.UpdateSettings("title", 2, 3));
        Assert.Equal(10, c.Capacity); c.UpdateSettings("  new title  ", 3, 3);
        Assert.Equal(3, c.Capacity); Assert.Equal("new title", c.Title);
    }
    [Fact]
    public void ArchivedClassCannotChangeSettings()
    { var c = NewClass(); c.Archive(DateTime.UtcNow); Assert.Throws<InvalidOperationException>(() => c.UpdateSettings("title", 10, 0)); }
    [Fact]
    public void CreationIntentIsImmutableAndLegacyHasNoGuessedIntent()
    {
        var c = NewClass(); Assert.Null(c.ClientOperationId); Assert.Null(c.CreationRequestHash);
        c.RecordCreationIntent(Guid.NewGuid(), Guid.NewGuid(), new string('A', 64));
        Assert.Throws<ArgumentException>(() => c.RecordCreationIntent(Guid.NewGuid(), Guid.NewGuid(), new string('B', 64)));
    }
    private static AcademicClass NewClass() => AcademicClass.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "title", 10, DateTime.UtcNow);
}
