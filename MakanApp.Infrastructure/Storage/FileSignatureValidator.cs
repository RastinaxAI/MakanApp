using System.Text;
using MakanApp.Application.Storage;

namespace MakanApp.Infrastructure.Storage;

public sealed class FileSignatureValidator : IFileContentValidator
{
    private const int InspectionLength = 512;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task ValidateAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!content.CanRead || !content.CanSeek)
        {
            throw Invalid();
        }

        var initialPosition = content.Position;
        var buffer = new byte[InspectionLength];
        var bytesRead = 0;
        try
        {
            while (bytesRead < buffer.Length)
            {
                var read = await content.ReadAsync(
                    buffer.AsMemory(bytesRead, buffer.Length - bytesRead),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }
        }
        finally
        {
            content.Position = initialPosition;
        }

        var sample = buffer.AsSpan(0, bytesRead);
        var valid = contentType switch
        {
            "application/pdf" => sample.StartsWith("%PDF-"u8),
            "image/jpeg" => sample.Length >= 3 && sample[0] == 0xFF && sample[1] == 0xD8 && sample[2] == 0xFF,
            "image/png" => sample.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "video/mp4" => sample.Length >= 12 && sample[4..8].SequenceEqual("ftyp"u8),
            "audio/mpeg" => IsMpegAudio(sample),
            "text/plain" => IsPlainText(sample),
            _ => false
        };
        if (!valid)
        {
            throw Invalid();
        }
    }

    private static bool IsMpegAudio(ReadOnlySpan<byte> sample) =>
        sample.StartsWith("ID3"u8) ||
        sample.Length >= 2 && sample[0] == 0xFF && (sample[1] & 0xE0) == 0xE0;

    private static bool IsPlainText(ReadOnlySpan<byte> sample)
    {
        if (sample.IsEmpty || sample.Contains((byte)0))
        {
            return false;
        }

        try
        {
            var value = StrictUtf8.GetString(sample);
            return value.All(character => !char.IsControl(character) || character is '\r' or '\n' or '\t');
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static StorageException Invalid() =>
        new(
            StorageErrorCodes.FileContentInvalid,
            "امضای محتوای فایل با نوع اعلام‌شده سازگار نیست.");
}
