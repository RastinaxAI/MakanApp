using MakanApp.Application.Reports.Exports;
using MakanApp.Infrastructure.Reports.Exports;
using Xunit;

namespace MakanApp.UnitTests.Reports;

public sealed class CsvCellEncoderTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"https://invalid\")")]
    [InlineData("+SUM(1,1)")]
    [InlineData("-1+2")]
    [InlineData("@cmd")]
    [InlineData("  =1+1")]
    public void UserControlledFormulaPrefixesAreNeutralized(string value)
    {
        var encoded = CsvCellEncoder.Encode(new ReportExportCell(value, true));

        Assert.Contains("'", encoded);
        Assert.DoesNotMatch("^\\s*[=+@-]", encoded.Trim('"'));
    }

    [Fact]
    public void NumericNegativeValueIsNotChangedWhenCellIsNotUserControlledText()
    {
        Assert.Equal("-17.25", CsvCellEncoder.Encode(new ReportExportCell("-17.25")));
    }

    [Fact]
    public void CommaQuoteNewlineAndPersianTextUseRfcStyleEscaping()
    {
        var encoded = CsvCellEncoder.Encode(new ReportExportCell("سلام،\"کلاس\"\nدوم", true));

        Assert.StartsWith("\"", encoded);
        Assert.EndsWith("\"", encoded);
        Assert.Contains("\"\"کلاس\"\"", encoded);
        Assert.Contains('\n', encoded);
    }

    [Fact]
    public void LongAllowedTextIsPreserved()
    {
        var value = new string('ی', 4_000);

        Assert.Equal(value, CsvCellEncoder.Encode(new ReportExportCell(value, true)));
    }
}
