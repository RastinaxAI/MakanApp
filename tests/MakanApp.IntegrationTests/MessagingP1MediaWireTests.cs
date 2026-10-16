using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(FileAssetStatus.Pending)]
    [InlineData(FileAssetStatus.Rejected)]
    [InlineData(FileAssetStatus.Deleted)]
    public async Task SharedMediaRetainedFileCannotLeaveReadyStateAndPaginationRemainsValid(FileAssetStatus status)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var ready = await UploadFileAsync(setup.FirstClient, "image/png", "p1-ready.png");
        var available = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [ready.Id] });
        var retained = await UploadFileAsync(setup.FirstClient, "image/png", "p1-retained.png");
        var newer = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [retained.Id] });
        var failure = await Record.ExceptionAsync(() => factory.SetFileAssetStatusAsync(retained.Id, status));
        var sqlFailure = failure as SqlException ?? failure?.InnerException as SqlException;
        Assert.NotNull(sqlFailure); Assert.Equal(547, sqlFailure.Number);
        Assert.Contains("CK_FileAssets_RetentionState", sqlFailure.Message);
        var page = (await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media?limit=1", JsonOptions))!;
        Assert.Equal(newer.MessageId, Assert.Single(page.Items).MessageId);
        Assert.NotNull(page.NextBeforeSequence);
        var previous = (await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media?limit=1&beforeSequence={page.NextBeforeSequence}", JsonOptions))!;
        Assert.Equal(available.MessageId, Assert.Single(previous.Items).MessageId); Assert.Null(previous.NextBeforeSequence);
        Assert.Equal(HttpStatusCode.OK, (await setup.SecondClient.GetAsync($"/api/v1/files/{retained.Id}/content")).StatusCode);
    }

    [Fact]
    public async Task PersianTextUploadAcrossSignatureBoundaryCanBeSentAndDownloadedPrivately()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var bytes = Encoding.UTF8.GetBytes(new string('a', 511) + "فارسی");
        using var form = new MultipartFormDataContent(); using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); form.Add(content, "File", "p1-persian.txt");
        using var upload = await setup.FirstClient.PostAsync("/api/v1/files", form);
        upload.EnsureSuccessStatusCode();
        var file = (await upload.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
        await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.File, AttachmentIds = [file.Id] });
        using var downloaded = await setup.SecondClient.GetAsync($"/api/v1/files/{file.Id}/content");
        downloaded.EnsureSuccessStatusCode();
        Assert.Equal(bytes, await downloaded.Content.ReadAsByteArrayAsync());
        using var outsider = factory.CreateClient(); await CreateMessagingUserAsync(outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/v1/files/{file.Id}/content")).StatusCode);
    }

    [Fact]
    public async Task MessagingJsonKeepsUtcDesignatorAfterSqlReloadAndIdempotentRetry()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var key = Guid.NewGuid();
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, key, "p1-wire-utc");
        using var retry = await SendMessageAsync(setup.FirstClient, setup.ConversationId, key, "p1-wire-utc");
        using var retryJson = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        AssertP1Utc(retryJson.RootElement, "sentAtUtc");
        using var history = JsonDocument.Parse(await setup.SecondClient.GetStringAsync($"/api/v1/conversations/{setup.ConversationId}/messages"));
        AssertP1Utc(history.RootElement.GetProperty("messages")[0], "sentAtUtc");
        using var delta = JsonDocument.Parse(await setup.SecondClient.GetStringAsync($"/api/v1/conversations/{setup.ConversationId}/changes"));
        foreach (var change in delta.RootElement.GetProperty("changes").EnumerateArray()) AssertP1Utc(change, "occurredAtUtc");
        using var search = JsonDocument.Parse(await setup.SecondClient.GetStringAsync("/api/v1/messages/search?q=p1-wire-utc"));
        AssertP1Utc(search.RootElement.GetProperty("items")[0], "sentAtUtc");
        using var list = JsonDocument.Parse(await setup.SecondClient.GetStringAsync("/api/v1/conversations"));
        AssertP1Utc(list.RootElement[0], "lastMessageAtUtc");
        var file = await UploadFileAsync(setup.FirstClient, "image/png", "p1-wire.png");
        var image = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [file.Id] });
        using var media = JsonDocument.Parse(await setup.SecondClient.GetStringAsync($"/api/v1/conversations/{setup.ConversationId}/media"));
        AssertP1Utc(media.RootElement.GetProperty("items")[0], "sentAtUtc");
        AssertP1Utc(media.RootElement.GetProperty("items")[0].GetProperty("attachment"), "createdAtUtc");
        (await setup.FirstClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("p1-wire-edited", sent.Version), JsonOptions)).EnsureSuccessStatusCode();
        using var edited = JsonDocument.Parse(await setup.SecondClient.GetStringAsync($"/api/v1/conversations/{setup.ConversationId}/messages"));
        AssertP1Utc(edited.RootElement.GetProperty("messages").EnumerateArray().Single(m => m.GetProperty("messageId").GetGuid() == sent.MessageId), "editedAtUtc");
        var mute = new SetConversationMuteCommand(ConversationMuteMode.Until, DateTime.UtcNow.AddHours(1));
        (await setup.SecondClient.PutAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/mute", mute, JsonOptions)).EnsureSuccessStatusCode();
        using var muteRetry = await setup.SecondClient.PutAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/mute", mute, JsonOptions);
        using var muteJson = JsonDocument.Parse(await muteRetry.Content.ReadAsStringAsync());
        AssertP1Utc(muteJson.RootElement, "mutedUntilUtc"); AssertP1Utc(muteJson.RootElement, "updatedAtUtc");
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", image.Sequence);
        using var readRetry = await setup.SecondClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/read", new AdvanceConversationCursorCommand(sent.Sequence), JsonOptions);
        using var readJson = JsonDocument.Parse(await readRetry.Content.ReadAsStringAsync()); AssertP1Utc(readJson.RootElement, "updatedAtUtc");
        var group = await CreateManagedAndReadAsync(setup.FirstClient, "groups", PersonalCommand("p1-wire-group"));
        using var details = JsonDocument.Parse(await setup.FirstClient.GetStringAsync($"/api/v1/conversations/{group.ConversationId}"));
        AssertP1Utc(details.RootElement, "createdAtUtc");
        AssertP1Utc(details.RootElement.GetProperty("participants")[0], "joinedAtUtc");
    }

    [Fact]
    public async Task RealtimeInvalidationUsesUtcTimestampAfterSqlReload()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await using var connection = CreateHubConnection(setup.Second.AccessToken);
        var received = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged, notification =>
        { if (notification.ConversationId == setup.ConversationId && notification.Type == MessagingChangeType.MessageCreated) received.TrySetResult(notification); });
        await connection.StartAsync(); await connection.InvokeAsync("SubscribeConversation", setup.ConversationId);
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "p1-wire-realtime");
        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(sent.MessageId, notification.ResourceId);
        Assert.Equal(DateTimeKind.Utc, notification.OccurredAtUtc.Kind);
        Assert.Equal(sent.SentAtUtc, notification.OccurredAtUtc);
    }

    private static void AssertP1Utc(JsonElement element, string property) => Assert.EndsWith("Z", element.GetProperty(property).GetString());

    [Theory]
    [InlineData("image/png", "image.png", MessageKind.Image)]
    [InlineData("video/mp4", "video.mp4", MessageKind.Video)]
    [InlineData("audio/mpeg", "voice.mp3", MessageKind.Voice)]
    [InlineData("application/pdf", "document.pdf", MessageKind.File)]
    [InlineData("text/plain", "document.txt", MessageKind.File)]
    public async Task SupportedMediaUploadSendDownloadAndSharedMediaUsePrivateFileAssetContract(string contentType, string fileName, MessageKind kind)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var file = await UploadFileAsync(setup.FirstClient, contentType, "p1-" + fileName);
        var sent = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = kind, AttachmentIds = [file.Id] });
        using var download = await setup.SecondClient.GetAsync($"/api/v1/files/{file.Id}/content");
        download.EnsureSuccessStatusCode(); Assert.Equal(ValidFileBytes(contentType), await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        var page = (await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media?kind={kind}&limit=1", JsonOptions))!;
        var item = Assert.Single(page.Items); Assert.Equal(sent.MessageId, item.MessageId); Assert.Equal(file.Id, item.Attachment.FileAssetId);
        Assert.Null(page.NextBeforeSequence);
    }

    [Fact]
    public async Task SharedMediaPagesKeepAllAttachmentsAndRespectForMeWithoutRewritingPeerHistory()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var olderFile = await UploadFileAsync(setup.FirstClient, "image/png", "p1-media-old.png");
        var older = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [olderFile.Id] });
        var firstFile = await UploadFileAsync(setup.FirstClient, "image/png", "p1-media-a.png");
        var secondFile = await UploadFileAsync(setup.FirstClient, "image/png", "p1-media-b.png");
        var newer = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId,
            new() { ClientMessageId = Guid.NewGuid(), Kind = MessageKind.Image, AttachmentIds = [firstFile.Id, secondFile.Id] });
        var first = (await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>($"/api/v1/conversations/{setup.ConversationId}/media?limit=1", JsonOptions))!;
        Assert.Equal(2, first.Items.Count); Assert.All(first.Items, item => Assert.Equal(newer.MessageId, item.MessageId));
        var previous = (await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media?limit=1&beforeSequence={first.NextBeforeSequence}", JsonOptions))!;
        Assert.Equal(older.MessageId, Assert.Single(previous.Items).MessageId); Assert.Null(previous.NextBeforeSequence);
        Assert.Equal(3, first.Items.Concat(previous.Items).Select(item => (item.MessageId, item.Attachment.FileAssetId)).Distinct().Count());
        (await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{newer.MessageId}/for-me")).EnsureSuccessStatusCode();
        var hidden = (await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>($"/api/v1/conversations/{setup.ConversationId}/media?limit=1", JsonOptions))!;
        Assert.Equal(older.MessageId, Assert.Single(hidden.Items).MessageId); Assert.Null(hidden.NextBeforeSequence);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.GetAsync($"/api/v1/files/{firstFile.Id}/content")).StatusCode);
        var peer = (await setup.FirstClient.GetFromJsonAsync<ConversationMediaPageResult>($"/api/v1/conversations/{setup.ConversationId}/media?limit=1", JsonOptions))!;
        Assert.Equal(2, peer.Items.Count);
    }
}
