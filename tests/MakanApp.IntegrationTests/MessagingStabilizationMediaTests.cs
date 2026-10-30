using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("image/png", "sample.png", MessageKind.Image)]
    [InlineData("image/jpeg", "sample.jpg", MessageKind.Image)]
    [InlineData("application/pdf", "sample.pdf", MessageKind.File)]
    [InlineData("text/plain", "sample.txt", MessageKind.File)]
    public async Task StabilizationEligibleAdultAttachmentHasPrivateBinaryHashRangeAndOneRetryEffect(string mime, string name, MessageKind kind)
    {
        // دو حساب Adult تأییدشده؛ این آزمون تأیید ایمنی رسانهٔ Minor/Unknown یا وجود scanner را ادعا نمی‌کند.
        using var setup = await CreateAdvancedConversationSetupAsync();
        var bytes = StabilizationMediaFixtures.Bytes(mime);
        using var form = new MultipartFormDataContent();
        using var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(part, "File", name);
        using var upload = await setup.FirstClient.PostAsync("/api/v1/files", form);
        upload.EnsureSuccessStatusCode();
        var file = (await upload.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
        await using var live = CreateHubConnection(setup.Second.AccessToken);
        var received = new TaskCompletionSource<MessagingRealtimeNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        live.On<MessagingRealtimeNotification>(MessagingRealtimeEventNames.ConversationChanged,
            e => { if (e.ConversationId == setup.ConversationId && e.Type == MessagingChangeType.MessageCreated) received.TrySetResult(e); });
        await live.StartAsync();
        var command = new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Kind = kind, AttachmentIds = [file.Id] };
        var first = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, command);
        var retry = await SendAdvancedMessageAsync(setup.FirstClient, setup.ConversationId, command);
        Assert.Equal(first.MessageId, retry.MessageId);
        Assert.Equal(first.MessageId, (await received.Task.WaitAsync(TimeSpan.FromSeconds(15))).ResourceId);
        var history = (await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/messages", JsonOptions))!;
        Assert.Equal(file.Id, Assert.Single(Assert.Single(history.Messages).Attachments).FileAssetId);
        using var download = await setup.SecondClient.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(mime, download.Content.Headers.ContentType!.MediaType);
        Assert.NotNull(download.Content.Headers.ContentDisposition);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(await download.Content.ReadAsByteArrayAsync()));
        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/files/{file.Id}/content");
        rangeRequest.Headers.Range = new RangeHeaderValue(1, 3);
        using var range = await setup.SecondClient.SendAsync(rangeRequest);
        Assert.Equal(HttpStatusCode.PartialContent, range.StatusCode);
        Assert.Equal(bytes[1..4], await range.Content.ReadAsByteArrayAsync());
        Assert.Equal(bytes.Length, range.Content.Headers.ContentRange!.Length);
        using var outsider = factory.CreateClient();
        _ = await CreateMessagingUserAsync(outsider);
        using var forbidden = await outsider.GetAsync($"/api/v1/files/{file.Id}/content");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }
}
