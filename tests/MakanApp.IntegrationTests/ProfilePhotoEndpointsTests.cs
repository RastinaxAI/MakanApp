using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    private bool _avatarHostPrepared;

    private HttpClient CreateAvatarClient()
    {
        if (!_avatarHostPrepared)
        {
            _host.Dispose();
            _host = factory.WithWebHostBuilder(builder =>
            {
                // فقط میزبان این تست‌ها JPEG را به allowlist محدود fixture اضافه می‌کند.
                builder.ConfigureTestServices(services =>
                {
                    var options = (StorageOptions)services.Last(item => item.ServiceType == typeof(StorageOptions)).ImplementationInstance!;
                    services.RemoveAll<StorageOptions>();
                    services.AddSingleton(new StorageOptions
                    {
                        RootPath = options.RootPath,
                        MaxFileSizeBytes = options.MaxFileSizeBytes,
                        UnattachedLifetime = options.UnattachedLifetime,
                        AllowedContentTypes = new HashSet<string>(options.AllowedContentTypes, StringComparer.OrdinalIgnoreCase) { "image/jpeg" }
                    });
                    services.RemoveAll<IHostedService>();
                });
            });
            _avatarHostPrepared = true;
        }
        return _host.CreateClient();
    }

    // تصاویر یک‌پیکسلی مصنوعی‌اند؛ هیچ عکس یا اطلاعات واقعی کاربر در fixture وجود ندارد.
    private const string AvatarPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jZ1kAAAAASUVORK5CYII=";
    private const string AvatarJpeg = "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==";

    [Fact]
    public async Task ProfilePhotoExistingAccountDefaultsToNullAndDoesNotChangeDobOrAge()
    {
        using var client = CreateAvatarClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var before = (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!;
        Assert.Null(before.ProfilePhoto);
        var photo = await UploadAvatarImageAsync(client);
        _ = await SetAvatarAsync(client, photo.Id);
        using var profile = await client.PatchAsJsonAsync("/api/v1/me/profile",
            new CompleteProfileCommand("نام", "نام خانوادگی", null, account.Username, new DateOnly(2000, 2, 29)));
        profile.EnsureSuccessStatusCode();
        var after = (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!;
        Assert.Equal(photo.Id, after.ProfilePhoto!.FileAssetId);
        Assert.Equal(new DateOnly(2000, 2, 29), after.DateOfBirth);
        await using var scope = _host.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Users.SingleAsync(u => u.Id == account.UserId);
        Assert.Equal(CommunicationAgeVerificationStatus.Unverified, user.CommunicationAgeVerificationStatus);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    public async Task ProfilePhotoOwnedImageWorksWithoutRoleOrOrganization(string contentType)
    {
        using var client = CreateAvatarClient();
        var account = await CreateP0PersonalAccountAsync(client, null);
        var file = await UploadAvatarImageAsync(client, contentType);
        var result = await SetAvatarAsync(client, file.Id);
        Assert.Equal(file.Id, result.FileAssetId);
        Assert.Equal(contentType, result.ContentType);
        using var download = await client.GetAsync(result.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(contentType, download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Convert.FromBase64String(contentType == "image/jpeg" ? AvatarJpeg : AvatarPng), await download.Content.ReadAsByteArrayAsync());
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.True(download.Headers.CacheControl.Private);
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.Memberships.AnyAsync(m => m.UserId == account.UserId));
        Assert.False(await db.PlatformRoleAssignments.AnyAsync(r => r.UserId == account.UserId));
        var stored = await db.FileAssets.SingleAsync(f => f.Id == file.Id);
        Assert.Equal(FileAssetStatus.Ready, stored.Status);
        Assert.Null(stored.OrganizationId);
    }

    [Theory]
    [InlineData("text/plain", "not-image.txt", "text")]
    [InlineData("application/pdf", "document.pdf", "%PDF-1.4\n")]
    public async Task ProfilePhotoRejectsOwnedNonImage(string type, string name, string text)
    {
        using var client = CreateAvatarClient(); _ = await CreateP0PersonalAccountAsync(client, null);
        var file = await UploadAvatarFileAsync(client, name, type, System.Text.Encoding.UTF8.GetBytes(text));
        await ErrorAsync(await client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(file.Id)),
            HttpStatusCode.UnsupportedMediaType, StorageErrorCodes.FileTypeNotAllowed);
        Assert.Null((await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.ProfilePhoto);
    }

    [Fact]
    public async Task ProfilePhotoCannotAssignAnotherUsersAsset()
    {
        using var first = CreateAvatarClient(); using var second = CreateAvatarClient();
        _ = await CreateP0PersonalAccountAsync(first, null); _ = await CreateP0PersonalAccountAsync(second, null);
        var file = await UploadAvatarImageAsync(second);
        await ErrorAsync(await first.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(file.Id)),
            HttpStatusCode.NotFound, StorageErrorCodes.FileNotFound);
        Assert.Null((await first.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.ProfilePhoto);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfilePhotoUnknownAndEmptyIdsReturnStableNotFound(bool empty)
    {
        using var client = CreateAvatarClient(); _ = await CreateP0PersonalAccountAsync(client, null);
        await ErrorAsync(await client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(empty ? Guid.Empty : Guid.NewGuid())),
            HttpStatusCode.NotFound, StorageErrorCodes.FileNotFound);
    }

    [Theory]
    [InlineData(FileAssetStatus.Pending)]
    [InlineData(FileAssetStatus.Rejected)]
    [InlineData(FileAssetStatus.Deleted)]
    public async Task ProfilePhotoRequiresReadyAsset(FileAssetStatus status)
    {
        using var client = CreateAvatarClient(); var account = await CreateP0PersonalAccountAsync(client, null);
        var file = FileAsset.CreatePending(Guid.NewGuid(), null, account.UserId, "pending.png", Guid.NewGuid().ToString("N"), "image/png", Now, Now.AddHours(1));
        if (status == FileAssetStatus.Rejected) file.MarkRejected(Now);
        if (status == FileAssetStatus.Deleted) file.MarkDeleted(Now);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); db.FileAssets.Add(file); await db.SaveChangesAsync();
        }
        await ErrorAsync(await client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(file.Id)),
            HttpStatusCode.Conflict, StorageErrorCodes.FileNotReady);
    }

    [Theory]
    [InlineData("image/svg+xml", "unsafe.svg")]
    [InlineData("image/webp", "unsupported.webp")]
    [InlineData("image/png", "false-image.png")]
    public async Task ProfilePhotoUploadRejectsUnsupportedOrForgedImage(string type, string name)
    {
        using var client = CreateAvatarClient(); var account = await CreateP0PersonalAccountAsync(client, null);
        using var form = new MultipartFormDataContent(); var bytes = new ByteArrayContent("not an image"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue(type); form.Add(bytes, "file", name);
        using var response = await client.PostAsync("/api/v1/files", form);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(0, await factory.CountFileAssetsForUserAsync(account.UserId));
    }

    [Fact]
    public async Task ProfilePhotoReplaceAndRemovePreserveFilesAndRevokeOldAvatarGrant()
    {
        using var owner = CreateAvatarClient(); _ = await OwnerAsync(owner);
        using var client = CreateAvatarClient(); using var viewer = CreateAvatarClient();
        _ = await CreateP0PersonalAccountAsync(client, owner); _ = await CreateP0PersonalAccountAsync(viewer, owner);
        var first = await UploadAvatarImageAsync(client); var second = await UploadAvatarImageAsync(client, "image/jpeg");
        var selected = await SetAvatarAsync(client, first.Id);
        using (var visible = await viewer.GetAsync(selected.DownloadUrl)) Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        var replaced = await SetAvatarAsync(client, second.Id);
        Assert.NotEqual(selected.DownloadUrl, replaced.DownloadUrl);
        using (var old = await viewer.GetAsync(selected.DownloadUrl)) Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        foreach (var id in new[] { first.Id, second.Id }) Assert.True(await factory.StoredBytesExistAsync(id));
        using (var removed = await client.DeleteAsync("/api/v1/me/profile-photo")) Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using (var repeated = await client.DeleteAsync("/api/v1/me/profile-photo")) Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Null((await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.ProfilePhoto);
        using (var old = await viewer.GetAsync(replaced.DownloadUrl)) Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
        Assert.True(await factory.StoredBytesExistAsync(second.Id));
    }

    [Fact]
    public async Task ProfilePhotoCannotBeDeletedThroughFileApiWhileActive()
    {
        using var client = CreateAvatarClient(); _ = await CreateP0PersonalAccountAsync(client, null);
        var file = await UploadAvatarImageAsync(client); _ = await SetAvatarAsync(client, file.Id);
        var metadata = (await client.GetFromJsonAsync<FileAssetResult>($"/api/v1/files/{file.Id}", PolicyJson))!;
        await ErrorAsync(await client.DeleteAsync($"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(metadata.RowVersion)}"),
            HttpStatusCode.Conflict, StorageErrorCodes.FileInUse);
        using (var removed = await client.DeleteAsync("/api/v1/me/profile-photo")) removed.EnsureSuccessStatusCode();
        using var deleted = await client.DeleteAsync($"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(metadata.RowVersion)}");
        deleted.EnsureSuccessStatusCode();
        Assert.False(await factory.StoredBytesExistAsync(file.Id));
    }

    [Fact]
    public async Task ProfilePhotoFlowsThroughPeopleContactsDirectAndHistoricalSender()
    {
        using var owner = CreateAvatarClient(); _ = await OwnerAsync(owner);
        using var first = CreateAvatarClient(); using var second = CreateAvatarClient();
        _ = await CreateP0PersonalAccountAsync(first, owner); var b = await CreateP0PersonalAccountAsync(second, owner);
        var file = await UploadAvatarImageAsync(second); _ = await SetAvatarAsync(second, file.Id);
        var people = (await first.GetFromJsonAsync<PeopleSearchPageResult>($"/api/v1/people/search?q={b.Username}", PolicyJson))!;
        Assert.Equal(file.Id, Assert.Single(people.Items).ProfilePhoto!.FileAssetId);
        using var save = await first.PostAsJsonAsync("/api/v1/contacts", new SaveUserContactCommand(b.UserId, null));
        save.EnsureSuccessStatusCode();
        var contacts = (await first.GetFromJsonAsync<UserContactPageResult>("/api/v1/contacts", PolicyJson))!;
        Assert.Equal(file.Id, Assert.Single(contacts.Items).Profile.ProfilePhoto!.FileAssetId);
        var direct = await CreateEpicPersonalDirectAsync(first, b.UserId);
        Assert.Equal(file.Id, direct.OtherParticipant.ProfilePhoto!.FileAssetId);
        _ = await SendP0MessageAsync(second, $"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام برای آزمون عکس فرستنده" });
        foreach (var replacement in new[] { true, false })
        {
            Guid? expected;
            if (replacement) { var next = await UploadAvatarImageAsync(second, "image/jpeg"); _ = await SetAvatarAsync(second, next.Id); expected = next.Id; }
            else { using var remove = await second.DeleteAsync("/api/v1/me/profile-photo"); remove.EnsureSuccessStatusCode(); expected = null; }
            var inbox = (await first.GetFromJsonAsync<ConversationSummaryResult[]>("/api/v1/conversations", PolicyJson))!;
            Assert.Equal(expected, Assert.Single(inbox).OtherParticipant!.ProfilePhoto?.FileAssetId);
            var history = (await first.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{direct.ConversationId}/messages", PolicyJson))!;
            Assert.Equal(expected, Assert.Single(history.Messages).Sender.ProfilePhoto?.FileAssetId);
        }
    }

    [Theory]
    [InlineData("groups")]
    [InlineData("channels")]
    public async Task ProfilePhotoAppearsInConversationMemberIdentity(string kind)
    {
        using var owner = CreateAvatarClient(); _ = await OwnerAsync(owner);
        using var client = CreateAvatarClient(); _ = await CreateP0PersonalAccountAsync(client, owner);
        var file = await UploadAvatarImageAsync(client); _ = await SetAvatarAsync(client, file.Id);
        var group = await CreateEpicPersonalManagedAsync(client, kind);
        var members = (await client.GetFromJsonAsync<ConversationParticipantPageResult>($"/api/v1/conversations/{group.ConversationId}/members", PolicyJson))!;
        Assert.Equal(file.Id, Assert.Single(members.Items).User.ProfilePhoto!.FileAssetId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfilePhotoDoesNotExposeUnknownOrMinorToStranger(bool verifiedMinor)
    {
        using var owner = CreateAvatarClient(); _ = await OwnerAsync(owner);
        using var child = CreateAvatarClient(); using var viewer = CreateAvatarClient();
        var target = await CreateP0PersonalAccountAsync(child, verifiedMinor ? owner : null, 2015);
        _ = await CreateP0PersonalAccountAsync(viewer, owner);
        var file = await UploadAvatarImageAsync(child); var photo = await SetAvatarAsync(child, file.Id);
        var found = (await viewer.GetFromJsonAsync<PeopleSearchPageResult>($"/api/v1/people/search?q={target.Username}", PolicyJson))!;
        Assert.Empty(found.Items);
        using var denied = await viewer.GetAsync(photo.DownloadUrl);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var direct = await viewer.PostAsJsonAsync("/api/v1/conversations/direct", new StartDirectConversationCommand(target.UserId, ConversationScope.Personal), PolicyJson);
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);
        Assert.Equal(file.Id, (await child.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.ProfilePhoto!.FileAssetId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfilePhotoSurvivesRoleAndOrganizationLifecycle(bool endMembership)
    {
        using var client = CreateAvatarClient(); var account = await CreateP0PersonalAccountAsync(client, null);
        var file = await UploadAvatarImageAsync(client); _ = await SetAvatarAsync(client, file.Id);
        var org = await factory.CreateOrganizationAsync($"Avatar {Guid.NewGuid():N}");
        var teacher = await factory.CreateMembershipAsync(account.UserId, org, OrganizationRole.Teacher);
        using (var selected = await client.PostAsJsonAsync("/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Organization, teacher.MembershipId, OrganizationRole.Teacher), PolicyJson)) selected.EnsureSuccessStatusCode();
        using (var download = await client.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        if (endMembership) await factory.EndMembershipAsync(teacher.MembershipId); else await factory.EndRoleAssignmentAsync(teacher.RoleAssignmentId);
        Assert.Equal(file.Id, (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.ProfilePhoto!.FileAssetId);
        _ = await SetAvatarAsync(client, file.Id);
        using (var personal = await client.PostAsJsonAsync("/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Personal, null, null), PolicyJson)) personal.EnsureSuccessStatusCode();
        using var content = await client.GetAsync($"/api/v1/files/{file.Id}/content"); Assert.Equal(HttpStatusCode.OK, content.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfilePhotoSuspensionOrRevokedSessionDeniesActionsWithoutDeletingFile(bool revoke)
    {
        using var client = CreateAvatarClient(); var account = await CreateP0PersonalAccountAsync(client, null);
        var file = await UploadAvatarImageAsync(client); _ = await SetAvatarAsync(client, file.Id);
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            if (revoke) await db.UserSessions.Where(s => s.UserId == account.UserId).ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, Now));
            else { (await db.Users.SingleAsync(u => u.Id == account.UserId)).Suspend(Now); await db.SaveChangesAsync(); }
        }
        using (var put = await client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(file.Id))) Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        using (var remove = await client.DeleteAsync("/api/v1/me/profile-photo")) Assert.Equal(HttpStatusCode.Unauthorized, remove.StatusCode);
        using (var content = await client.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.Unauthorized, content.StatusCode);
        await using var check = _host.Services.CreateAsyncScope();
        var dbCheck = check.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(file.Id, (await dbCheck.Users.SingleAsync(u => u.Id == account.UserId)).ProfilePhotoFileAssetId);
        Assert.Equal(FileAssetStatus.Ready, (await dbCheck.FileAssets.SingleAsync(f => f.Id == file.Id)).Status);
    }

    [Fact]
    public async Task ProfilePhotoCannotUsePathsUrlsOrAnonymousAccessAndDoesNotLeakStorage()
    {
        using var client = CreateAvatarClient(); _ = await CreateP0PersonalAccountAsync(client, null);
        using var injected = await client.PutAsJsonAsync("/api/v1/me/profile-photo", new { storagePath = "C:\\private\\photo.png", url = "https://untrusted.example/photo.png" });
        Assert.Equal(HttpStatusCode.NotFound, injected.StatusCode);
        var file = await UploadAvatarImageAsync(client); _ = await SetAvatarAsync(client, file.Id);
        var json = await client.GetStringAsync("/api/v1/me");
        Assert.DoesNotContain(factory.StorageRootPath, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("untrusted.example", json, StringComparison.OrdinalIgnoreCase);
        using var anonymous = CreateAvatarClient();
        using (var get = await anonymous.GetAsync($"/api/v1/files/{file.Id}/content")) Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        using (var put = await anonymous.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(file.Id))) Assert.Equal(HttpStatusCode.Unauthorized, put.StatusCode);
        using (var remove = await anonymous.DeleteAsync("/api/v1/me/profile-photo")) Assert.Equal(HttpStatusCode.Unauthorized, remove.StatusCode);
    }

    [Fact]
    public async Task ProfilePhotoOpenApiIsAdditiveAndPresenceRemainsStateOnly()
    {
        using var client = CreateAvatarClient(); using var api = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var schemas = api.RootElement.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { nameof(CurrentUserResult), nameof(SafeMessagingIdentityResult), nameof(MessagingPublicProfileResult) })
            Assert.True(schemas.GetProperty(name).GetProperty("properties").TryGetProperty("profilePhoto", out _));
        Assert.True(api.RootElement.GetProperty("paths").GetProperty("/api/v1/me/profile-photo").TryGetProperty("put", out _));
        Assert.True(api.RootElement.GetProperty("paths").GetProperty("/api/v1/me/profile-photo").TryGetProperty("delete", out _));
        var photo = schemas.GetProperty(nameof(ProfilePhotoResult)).GetProperty("properties");
        Assert.True(photo.TryGetProperty("fileAssetId", out _)); Assert.True(photo.TryGetProperty("downloadUrl", out _));
        Assert.False(photo.TryGetProperty("storageKey", out _));
        Assert.DoesNotContain(typeof(PresenceResult).GetProperties(), p => p.Name == "ProfilePhoto");
    }

    [Fact]
    public async Task ProfilePhotoRejectsOrganizationScopedFileEvenWhenUploaderIsTheActor()
    {
        using var client = CreateAvatarClient(); var account = await CreateP0PersonalAccountAsync(client, null);
        var org = await factory.CreateOrganizationAsync($"Asset scope {Guid.NewGuid():N}");
        var teacher = await factory.CreateMembershipAsync(account.UserId, org, OrganizationRole.Teacher);
        using (var selected = await client.PostAsJsonAsync("/api/v1/workspaces/select", new SelectWorkspaceCommand(WorkspaceType.Organization, teacher.MembershipId, OrganizationRole.Teacher), PolicyJson)) selected.EnsureSuccessStatusCode();
        var file = await UploadAvatarImageAsync(client);
        Assert.Equal(org, file.OrganizationId);
        await ErrorAsync(await client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(file.Id)),
            HttpStatusCode.NotFound, StorageErrorCodes.FileNotFound);
    }

    [Fact]
    public async Task ProfilePhotoUploadUsesConfiguredStorageLimit()
    {
        using var client = CreateAvatarClient(); _ = await CreateP0PersonalAccountAsync(client, null);
        var limit = _host.Services.GetRequiredService<StorageOptions>().MaxFileSizeBytes;
        var bytes = new byte[checked((int)limit + 1)]; Convert.FromBase64String(AvatarPng).CopyTo(bytes, 0);
        using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(content, "file", "too-large.png");
        await ErrorAsync(await client.PostAsync("/api/v1/files", form), HttpStatusCode.RequestEntityTooLarge, StorageErrorCodes.FileTooLarge);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfilePhotoDoesNotBypassPrivacyOrBlockForNewMessages(bool block)
    {
        using var owner = CreateAvatarClient(); _ = await OwnerAsync(owner);
        using var first = CreateAvatarClient(); using var second = CreateAvatarClient();
        var a = await CreateP0PersonalAccountAsync(first, owner); var b = await CreateP0PersonalAccountAsync(second, owner);
        var file = await UploadAvatarImageAsync(second); _ = await SetAvatarAsync(second, file.Id);
        var direct = await CreateEpicPersonalDirectAsync(first, b.UserId);
        if (block) { using var blocked = await second.PostAsync($"/api/v1/messaging/blocks/{a.UserId}", null); blocked.EnsureSuccessStatusCode(); }
        else
        {
            using var privacy = await second.PutAsJsonAsync("/api/v1/messaging/privacy",
                new UpdateMessagingPrivacyCommand(PrivacyAudience.Nobody, PrivacyAudience.Nobody, PrivacyAudience.Nobody, PrivacyAudience.Nobody, null), PolicyJson);
            privacy.EnsureSuccessStatusCode();
        }
        await ErrorAsync(await first.PostAsJsonAsync($"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام جدید نباید پذیرفته شود" }),
            HttpStatusCode.Forbidden, MessagingErrorCodes.MessageNotAllowed);
        // هویت موجود در سیاست جاری ممکن است قابل نمایش بماند؛ Avatar مجوز Send یا افشای تلفن نمی‌سازد.
        var people = (await first.GetFromJsonAsync<PeopleSearchPageResult>($"/api/v1/people/search?q={b.Username}", PolicyJson))!;
        var profile = Assert.Single(people.Items); Assert.False(profile.CanStartDirect); Assert.Null(profile.PhoneNumber);
        Assert.Equal(file.Id, profile.ProfilePhoto!.FileAssetId);
    }

    [Fact]
    public async Task ProfilePhotoHistoricalIdentityRequiresAnActuallyReadableConversation()
    {
        using var owner = CreateAvatarClient(); _ = await OwnerAsync(owner);
        using var first = CreateAvatarClient(); using var second = CreateAvatarClient(); using var stranger = CreateAvatarClient();
        _ = await CreateP0PersonalAccountAsync(first, owner); var b = await CreateP0PersonalAccountAsync(second, owner);
        _ = await CreateP0PersonalAccountAsync(stranger, owner);
        var file = await UploadAvatarImageAsync(second); var photo = await SetAvatarAsync(second, file.Id);
        var direct = await CreateEpicPersonalDirectAsync(first, b.UserId);
        _ = await SendP0MessageAsync(second, $"/api/v1/conversations/{direct.ConversationId}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "تاریخچهٔ مجاز پیش از تغییر وضعیت" });
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Users.SingleAsync(u => u.Id == b.UserId)).SetCommunicationAgeCategory(CommunicationAgeCategory.Unknown, Now);
            await db.SaveChangesAsync();
        }
        using (var denied = await stranger.GetAsync(photo.DownloadUrl)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var history = await first.GetAsync($"/api/v1/conversations/{direct.ConversationId}/messages")) history.EnsureSuccessStatusCode();
        using (var allowed = await first.GetAsync(photo.DownloadUrl)) Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using (var denied = await stranger.GetAsync($"/api/v1/conversations/{direct.ConversationId}/messages")) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }

    [Fact]
    public async Task ProfilePhotoBindingFencesAStaleFileDeletionWithSqlRowversion()
    {
        using var client = CreateAvatarClient(); _ = await CreateP0PersonalAccountAsync(client, null);
        var file = await UploadAvatarImageAsync(client);
        await using var staleScope = _host.Services.CreateAsyncScope();
        var staleStore = staleScope.ServiceProvider.GetRequiredService<IFileAssetStore>();
        var stale = (await staleStore.GetForUpdateAsync(file.Id, CancellationToken.None))!;
        _ = await SetAvatarAsync(client, file.Id);
        stale.MarkDeleted(Now);
        var conflict = await Assert.ThrowsAsync<StorageException>(() => staleStore.SaveChangesAsync(CancellationToken.None));
        Assert.Equal(StorageErrorCodes.ConcurrencyConflict, conflict.Code);
        Assert.True(await factory.StoredBytesExistAsync(file.Id));
        Assert.Equal(file.Id, (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!.ProfilePhoto!.FileAssetId);
    }

    [Fact]
    public async Task ProfilePhotoConcurrentSelectionsLeaveExactlyOneReadyReference()
    {
        using var client = CreateAvatarClient(); var account = await CreateP0PersonalAccountAsync(client, null);
        var first = await UploadAvatarImageAsync(client); var second = await UploadAvatarImageAsync(client, "image/jpeg");
        var replies = await Task.WhenAll(client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(first.Id)),
            client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(second.Id)));
        foreach (var response in replies) { using (response) response.EnsureSuccessStatusCode(); }
        var result = (await client.GetFromJsonAsync<CurrentUserResult>("/api/v1/me"))!;
        Assert.Contains(result.ProfilePhoto!.FileAssetId, new[] { first.Id, second.Id });
        await using var scope = _host.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Id == account.UserId && u.ProfilePhotoFileAssetId != null));
        Assert.Equal(FileAssetStatus.Ready, (await db.FileAssets.SingleAsync(f => f.Id == result.ProfilePhoto.FileAssetId)).Status);
    }

    private static async Task<ProfilePhotoResult> SetAvatarAsync(HttpClient client, Guid fileId)
    {
        using var response = await client.PutAsJsonAsync("/api/v1/me/profile-photo", new SetProfilePhotoCommand(fileId));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ProfilePhotoResult>())!;
    }

    private static Task<FileAssetResult> UploadAvatarImageAsync(HttpClient client, string type = "image/png") =>
        UploadAvatarFileAsync(client, type == "image/jpeg" ? "avatar.jpg" : "avatar.png", type,
            Convert.FromBase64String(type == "image/jpeg" ? AvatarJpeg : AvatarPng));

    private static async Task<FileAssetResult> UploadAvatarFileAsync(HttpClient client, string name, string type, byte[] bytes)
    {
        using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type); form.Add(content, "file", name);
        using var response = await client.PostAsync("/api/v1/files", form); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileAssetResult>(PolicyJson))!;
    }
}
