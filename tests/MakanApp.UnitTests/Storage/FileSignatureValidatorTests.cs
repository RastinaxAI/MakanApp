using System.Text;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Storage;
using Xunit;

namespace MakanApp.UnitTests.Storage;

public sealed class FileSignatureValidatorTests
{
    private readonly FileSignatureValidator _validator = new();

    [Theory]
    [InlineData("application/pdf", "%PDF-1.7\n")]
    [InlineData("text/plain", "متن معتبر UTF-8")]
    public async Task ValidSignaturesAreAcceptedAndStreamPositionIsRestored(
        string contentType,
        string value)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));

        await _validator.ValidateAsync(stream, contentType, CancellationToken.None);

        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task ClaimedPngWithExecutableContentIsRejected()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("MZ executable"));

        var exception = await Assert.ThrowsAsync<StorageException>(() =>
            _validator.ValidateAsync(stream, "image/png", CancellationToken.None));

        Assert.Equal(StorageErrorCodes.FileContentInvalid, exception.Code);
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData(511, "فارسی")]
    [InlineData(510, "😀 متن")]
    public async Task ValidUtf8SplitAtInspectionBoundaryIsAccepted(int prefixLength, string suffix)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', prefixLength) + suffix));
        await _validator.ValidateAsync(stream, "text/plain", default);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task IncompleteUtf8AtActualEndOfFileRemainsRejected()
    {
        await using var stream = new MemoryStream(Enumerable.Repeat((byte)'a', 511).Append((byte)0xD9).ToArray());
        await Assert.ThrowsAsync<StorageException>(() => _validator.ValidateAsync(stream, "text/plain", default));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task InvalidUtf8InsideTruncatedSampleRemainsRejected()
    {
        var bytes = Enumerable.Repeat((byte)'a', 600).ToArray(); bytes[30] = 0xFF;
        await using var stream = new MemoryStream(bytes);
        await Assert.ThrowsAsync<StorageException>(() => _validator.ValidateAsync(stream, "text/plain", default));
        Assert.Equal(0, stream.Position);
    }

    [Theory]
    [InlineData("image/jpeg", "FFD8FFE0")]
    [InlineData("image/png", "89504E470D0A1A0A01")]
    [InlineData("video/mp4", "000000106674797069736F6D")]
    [InlineData("audio/mpeg", "494433617564696F")]
    public async Task CurrentlySupportedMediaSignaturesAreAcceptedWithoutClaimingDecoderOrMalwareValidation(string type, string hex)
    {
        await using var stream = new MemoryStream(Convert.FromHexString(hex));
        await _validator.ValidateAsync(stream, type, default);
        Assert.Equal(0, stream.Position);
    }
}
