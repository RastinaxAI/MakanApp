using MakanApp.Application.Identity;
using Xunit;

namespace MakanApp.UnitTests.Identity;

public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void DevelopmentSeedIranianPhones_NormalizeToTheirCanonicalIdentifiers(int number)
    {
        var suffix = (100 + number).ToString("D3");
        var localPhone = "09120000" + suffix;
        var canonicalPhone = "+989120000" + suffix;

        Assert.True(PhoneNumberNormalizer.TryNormalize(localPhone, out var normalized));
        Assert.Equal(canonicalPhone, normalized);
        Assert.True(PhoneNumberNormalizer.TryNormalize(canonicalPhone, out var roundTrip));
        Assert.Equal(canonicalPhone, roundTrip);
    }

    [Theory]
    [InlineData("0912 123 4567", "+989121234567")]
    [InlineData("+98 (912) 123-4567", "+989121234567")]
    [InlineData("۰۰۹۸۹۱۲۱۲۳۴۵۶۷", "+989121234567")]
    [InlineData("989121234567", "+989121234567")]
    public void TryNormalizeAcceptsSupportedMobileFormats(string input, string expected)
    {
        var succeeded = PhoneNumberNormalizer.TryNormalize(input, out var normalized);

        Assert.True(succeeded);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0912")]
    [InlineData("phone")]
    [InlineData("9121234567")]
    public void TryNormalizeRejectsInvalidValues(string input)
    {
        Assert.False(PhoneNumberNormalizer.TryNormalize(input, out _));
    }
}
