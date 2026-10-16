using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2PresentationLabelReadsObserveWholeVersionDuringReplacement()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        var saved = (await client.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var message = await SendMessageAndReadAsync(client, saved.ConversationId, Guid.NewGuid(), "نسخهٔ سازگار");
        var before = await ReadP2LabelsAsync(client, message.MessageId);
        for (var round = 0; round < 8; round++)
        {
            var labels = new[] { "A" + round, "B" + round }; var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<SavedMessageLabelsResult> Read() { await start.Task; return await ReadP2LabelsAsync(client, message.MessageId); }
            async Task<SavedMessageLabelsResult> Write() { await start.Task; return await WriteP2LabelsAsync(client, message.MessageId, labels, before.Version); }
            var read = Read(); var write = Write(); start.SetResult(); await Task.WhenAll(read, write);
            var observed = await read; var after = await write;
            Assert.Contains(observed.Version, new[] { before.Version, after.Version });
            Assert.Equal(observed.Version == before.Version ? before.Labels : after.Labels, observed.Labels);
            before = after;
        }
    }

    [Fact]
    public async Task P2PresentationFolderReadDoesNotMixParentVersionAndOverrides()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var id = Guid.NewGuid();
        var before = await WriteP2FolderAsync(setup.FirstClient, id, P2Folder("اول", [new(setup.ConversationId, true)]));
        for (var round = 0; round < 8; round++)
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var command = P2Folder("نام " + round, [new(setup.ConversationId, round % 2 == 0)]) with { ExpectedVersion = before.Version };
            async Task<ChatFolderResult> Read()
            { await start.Task; return Assert.Single((await setup.FirstClient.GetFromJsonAsync<ChatFolderResult[]>("/api/v1/messaging/folders", JsonOptions))!); }
            async Task<ChatFolderResult> Write() { await start.Task; return await WriteP2FolderAsync(setup.FirstClient, id, command); }
            var read = Read(); var write = Write(); start.SetResult(); await Task.WhenAll(read, write);
            var observed = await read; var after = await write; var expected = observed.Version == before.Version ? before : after;
            Assert.Contains(observed.Version, new[] { before.Version, after.Version }); Assert.Equal(expected.Name, observed.Name);
            Assert.Equal(expected.Overrides, observed.Overrides); before = after;
        }
    }

    [Fact]
    public async Task P2PresentationLabelsAndMessageEditUseConsistentConversationLockOrder()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        var saved = (await client.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var message = await SendMessageAndReadAsync(client, saved.ConversationId, Guid.NewGuid(), "متن اول");
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<SavedMessageLabelsResult> Labels() { await start.Task; return await WriteP2LabelsAsync(client, message.MessageId, ["درس"], null); }
        async Task<HttpResponseMessage> Edit()
        { await start.Task; return await client.PatchAsJsonAsync($"/api/v1/conversations/{saved.ConversationId}/messages/{message.MessageId}", new EditMessageCommand("متن دوم", message.Version), JsonOptions); }
        var labels = Labels(); var edit = Edit(); start.SetResult(); await Task.WhenAll(labels, edit);
        using var response = await edit; response.EnsureSuccessStatusCode(); Assert.Single((await labels).Labels);
        Assert.Equal("متن دوم", Assert.Single(await factory.GetMessagesAsync(saved.ConversationId)).Text);
    }

    [Fact]
    public async Task P2PresentationRejectsOversizedRawTextBeforeNormalization()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        var oversized = new string(' ', 1025) + "x";
        var folder = await client.PutAsJsonAsync($"/api/v1/messaging/folders/{Guid.NewGuid()}", P2Folder(oversized), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, folder.StatusCode); Assert.Equal(MessagingErrorCodes.PresentationInvalid, await ReadProblemCodeAsync(folder));
        var saved = (await client.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var message = await SendMessageAndReadAsync(client, saved.ConversationId, Guid.NewGuid(), "محدودیت ورودی");
        var labels = await client.PutAsJsonAsync(P2LabelRoute(message.MessageId), new ReplaceSavedLabelsCommand([oversized], null), JsonOptions);
        Assert.Equal(HttpStatusCode.BadRequest, labels.StatusCode); Assert.Equal(MessagingErrorCodes.PresentationInvalid, await ReadProblemCodeAsync(labels));
        Assert.Null((await ReadP2LabelsAsync(client, message.MessageId)).Version);
    }
}
