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
}
