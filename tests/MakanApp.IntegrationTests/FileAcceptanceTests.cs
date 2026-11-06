using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using MakanApp.Application.Storage;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class FileStorageEndpointsTests
{
    [Theory]
    [InlineData("qa-small.txt", "text/plain")]
    [InlineData("qa-image.png", "image/png")]
    [InlineData("qa-document.pdf", "application/pdf")]
    public async Task FileAcceptanceRealBytesMetadataRangeAndDeletion(string name, string mime)
    {
        using var client = factory.CreateClient();
        var owner = await AuthenticateAsync(client);
        var bytes = StabilizationMediaFixtures.Bytes(mime);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        using var upload = await UploadAsync(client, name, mime, bytes);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var file = await ReadRequiredAsync<FileAssetResult>(upload);
        Assert.NotEqual(Guid.Empty, file.Id);
        Assert.Equal(name, file.OriginalFileName);
        Assert.Equal(mime, file.ContentType);
        Assert.Equal(bytes.Length, file.SizeBytes);
        Assert.Equal(FileAssetStatus.Ready, file.Status);
        Assert.Equal(owner.User.Id, file.UploadedByUserId);
        Assert.Null(file.OrganizationId);
        Assert.Equal(8, Convert.FromBase64String(file.RowVersion).Length);
        Assert.Equal(hash, file.Sha256Hash, ignoreCase: true);
        Assert.True(file.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-1));

        using var metadata = await client.GetAsync($"/api/v1/files/{file.Id}");
        Assert.Equal(file, await ReadRequiredAsync<FileAssetResult>(metadata));
        using var download = await client.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(mime, download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes.Length, download.Content.Headers.ContentLength);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl?.NoStore);
        Assert.Contains("Authorization", download.Headers.Vary);
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(await download.Content.ReadAsByteArrayAsync())));

        using var range = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{file.Id}/content");
        range.Headers.Range = new RangeHeaderValue(0, 9);
        using var partial = await client.SendAsync(range);
        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(10, partial.Content.Headers.ContentLength);
        Assert.Equal(bytes.Length, partial.Content.Headers.ContentRange!.Length);
        Assert.Equal(bytes[..10], await partial.Content.ReadAsByteArrayAsync());
        using var invalidRange = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{file.Id}/content");
        invalidRange.Headers.Range = new RangeHeaderValue(bytes.Length + 1, null);
        using var unsatisfied = await client.SendAsync(invalidRange);
        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, unsatisfied.StatusCode);

        using var deleted = await client.DeleteAsync($"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(file.RowVersion)}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        foreach (var suffix in new[] { "", "/content" })
        {
            using var missing = await client.GetAsync($"/api/v1/files/{file.Id}{suffix}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal("application/problem+json", missing.Content.Headers.ContentType!.MediaType);
        }
    }

    [Theory]
    [InlineData("report.exe", "text/plain")]
    [InlineData("report.png", "text/plain")]
    [InlineData("report.txt", "application/pdf")]
    public async Task FileAcceptanceRejectsExtensionMimeMismatchWithoutPersistence(string name, string mime)
    {
        using var client = factory.CreateClient();
        var owner = await AuthenticateAsync(client);
        using var response = await UploadAsync(client, name, mime, StabilizationMediaFixtures.Bytes(mime));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(StorageErrorCodes.FileTypeNotAllowed, await ReadProblemCodeAsync(response));
        Assert.Equal(0, await factory.CountFileAssetsForUserAsync(owner.User.Id));
    }

    [Fact]
    public async Task FileAcceptanceBoundaryUnicodeAndDuplicateNamesNeverOverwriteBytes()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var bytes = Bytes(new string('a', 1024));
        const string name = "گزارش تمرین.txt";
        using var firstResponse = await UploadAsync(client, name, "text/plain", bytes);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await ReadRequiredAsync<FileAssetResult>(firstResponse);
        using var secondResponse = await UploadAsync(client, name, "text/plain", Bytes("different bytes"));
        var second = await ReadRequiredAsync<FileAssetResult>(secondResponse);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(name, first.OriginalFileName);
        using var content = await client.GetAsync($"/api/v1/files/{first.Id}/content");
        Assert.Equal(bytes, await content.Content.ReadAsByteArrayAsync());
        using var over = await UploadAsync(client, name, "text/plain", Bytes(new string('b', 1025)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
    }

    [Fact]
    public async Task FileAcceptanceRevokedSessionCannotUploadOrReuseExistingDownloadUrl()
    {
        using var client = factory.CreateClient();
        var owner = await AuthenticateAsync(client);
        using var upload = await UploadAsync(client, "session.txt", "text/plain", Bytes("session"));
        var file = await ReadRequiredAsync<FileAssetResult>(upload);
        using (var before = await client.GetAsync($"/api/v1/files/{file.Id}/content"))
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        await factory.RevokeSessionByTokenAsync(owner.AccessToken);
        using var deniedUpload = await UploadAsync(client, "revoked.txt", "text/plain", Bytes("no"));
        Assert.Equal(HttpStatusCode.Unauthorized, deniedUpload.StatusCode);
        using var deniedDownload = await client.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.Unauthorized, deniedDownload.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-session");
        using var invalid = await UploadAsync(client, "invalid.txt", "text/plain", Bytes("no"));
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
    }
}
