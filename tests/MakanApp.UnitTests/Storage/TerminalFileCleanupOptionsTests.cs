using MakanApp.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MakanApp.UnitTests.Storage;

public sealed class TerminalFileCleanupOptionsTests
{
    [Fact]
    public void MissingConfigurationIsDisabledWithoutInventingRetentionPeriod()
    {
        var options = TerminalFileCleanupOptions.FromConfiguration(Configuration());

        Assert.False(options.Enabled);
        Assert.Null(options.GracePeriodHours);
        Assert.Equal(25, options.BatchSize);
        Assert.Equal(60, options.IntervalSeconds);
        Assert.Equal("Storage:TerminalCleanup", TerminalFileCleanupOptions.SectionName);
        options.Validate();
    }

    [Fact]
    public void ObjectDefaultsAlsoRemainDisabledWithoutGracePeriod()
    {
        var options = new TerminalFileCleanupOptions();

        Assert.False(options.Enabled);
        Assert.Null(options.GracePeriodHours);
        options.Validate();
    }

    [Fact]
    public void EnabledConfigurationRequiresExplicitGracePeriod()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TerminalFileCleanupOptions.FromConfiguration(Configuration(("Enabled", "true"))));
        Assert.Throws<InvalidOperationException>(() =>
            new TerminalFileCleanupOptions { Enabled = true }.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void InvalidGracePeriodIsRejectedEvenWhenDisabled(int gracePeriodHours)
    {
        Assert.Throws<InvalidOperationException>(() => new TerminalFileCleanupOptions
        {
            Enabled = false,
            GracePeriodHours = gracePeriodHours
        }.Validate());
    }

    public static TheoryData<int> PositiveGracePeriods => new()
    {
        1,
        TerminalFileCleanupOptions.MaximumGraceHours
    };

    [Theory]
    [MemberData(nameof(PositiveGracePeriods))]
    public void ExplicitPositiveGracePeriodWithinDateTimeRangeIsAccepted(int gracePeriodHours)
    {
        var options = new TerminalFileCleanupOptions
        {
            Enabled = true,
            GracePeriodHours = gracePeriodHours
        };

        options.Validate();

        Assert.Equal(gracePeriodHours, options.GracePeriodHours);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void BatchSizeOutsideBoundedRangeIsRejected(int batchSize)
    {
        Assert.Throws<InvalidOperationException>(() => new TerminalFileCleanupOptions
        {
            BatchSize = batchSize
        }.Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(TerminalFileCleanupOptions.MaximumBatchSize)]
    public void BatchSizeBoundaryIsAccepted(int batchSize)
    {
        new TerminalFileCleanupOptions { BatchSize = batchSize }.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(3601)]
    public void IntervalOutsideBoundedRangeIsRejected(int intervalSeconds)
    {
        Assert.Throws<InvalidOperationException>(() => new TerminalFileCleanupOptions
        {
            IntervalSeconds = intervalSeconds
        }.Validate());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(3600)]
    public void IntervalBoundaryIsAccepted(int intervalSeconds)
    {
        new TerminalFileCleanupOptions { IntervalSeconds = intervalSeconds }.Validate();
    }

    [Theory]
    [InlineData("Enabled")]
    [InlineData("GracePeriodHours")]
    [InlineData("BatchSize")]
    [InlineData("IntervalSeconds")]
    public void MalformedSettingIsRejectedWithoutEchoingItsValue(string setting)
    {
        const string privateMarker = "NOT_A_VALUE_PRIVATE_TEST_MARKER";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TerminalFileCleanupOptions.FromConfiguration(Configuration((setting, privateMarker))));

        Assert.DoesNotContain(privateMarker, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Enabled", "1")]
    [InlineData("GracePeriodHours", "0")]
    [InlineData("GracePeriodHours", "2147483647")]
    [InlineData("BatchSize", "101")]
    [InlineData("IntervalSeconds", "3601")]
    public void InvalidParsedSettingIsNotSilentlyReplacedWithDefault(string setting, string value)
    {
        Assert.Throws<InvalidOperationException>(() =>
            TerminalFileCleanupOptions.FromConfiguration(Configuration((setting, value))));
    }

    [Fact]
    public void ExplicitConfigurationPreservesAllApprovedBounds()
    {
        var options = TerminalFileCleanupOptions.FromConfiguration(Configuration(
            ("Enabled", "true"),
            ("GracePeriodHours", "1"),
            ("BatchSize", "100"),
            ("IntervalSeconds", "5")));

        Assert.True(options.Enabled);
        Assert.Equal(1, options.GracePeriodHours);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(5, options.IntervalSeconds);
    }

    private static IConfiguration Configuration(params (string Setting, string? Value)[] settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings.Select(setting =>
            new KeyValuePair<string, string?>(
                $"Storage:TerminalCleanup:{setting.Setting}", setting.Value))).Build();
}
