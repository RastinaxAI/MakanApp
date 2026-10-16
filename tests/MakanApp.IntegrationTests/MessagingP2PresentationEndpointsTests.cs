using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2PresentationLabelsUseCanonicalSavedAndConvergeAcrossSessions()
    {
        using var client = factory.CreateClient(); using var device = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await AuthenticateP1SessionAsync(device, user.PhoneNumber, factory.Services);
        var saved = (await client.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var message = await SendMessageAndReadAsync(client, saved.ConversationId, Guid.NewGuid(), "یادداشت برچسب‌دار");
        var empty = await ReadP2LabelsAsync(client, message.MessageId); Assert.Empty(empty.Labels); Assert.Null(empty.Version);
        var labels = await WriteP2LabelsAsync(client, message.MessageId, [" lesson ", "LESSON", "تمرین"], null);
        Assert.Equal(2, labels.Labels.Count); Assert.NotNull(labels.Version);
        var second = await ReadP2LabelsAsync(device, message.MessageId); Assert.Equal(labels.Labels, second.Labels); Assert.Equal(labels.Version, second.Version);
        Assert.Equal(saved.ConversationId, (await device.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!.ConversationId);
        Assert.Equal(1, await factory.CountSavedConversationsAsync(user.UserId));
        var cleared = await WriteP2LabelsAsync(device, message.MessageId, [], second.Version);
        Assert.Empty((await ReadP2LabelsAsync(client, message.MessageId)).Labels); Assert.NotEqual(second.Version, cleared.Version);
    }

    [Fact]
    public async Task P2PresentationLabelsRejectOtherOwnerOrdinaryChatAndHiddenSaved()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var saved = (await setup.FirstClient.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var own = await SendMessageAndReadAsync(setup.FirstClient, saved.ConversationId, Guid.NewGuid(), "private saved label");
        var ordinary = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "ordinary chat");
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.GetAsync(P2LabelRoute(own.MessageId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.PutAsJsonAsync(P2LabelRoute(own.MessageId), new ReplaceSavedLabelsCommand(["x"], null), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.PutAsJsonAsync(P2LabelRoute(ordinary.MessageId), new ReplaceSavedLabelsCommand(["x"], null), JsonOptions)).StatusCode);
        await WriteP2LabelsAsync(setup.FirstClient, own.MessageId, ["hidden"], null);
        (await setup.FirstClient.DeleteAsync($"/api/v1/conversations/{saved.ConversationId}/messages/{own.MessageId}/for-me")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.GetAsync(P2LabelRoute(own.MessageId))).StatusCode);
        Assert.Empty((await setup.FirstClient.GetFromJsonAsync<MessageSearchPageResult>("/api/v1/messages/search?savedLabel=hidden", JsonOptions))!.Items);
    }

    [Fact]
    public async Task P2PresentationLabelSearchCannotEscapeOwnerOrConversationScope()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var saved = (await setup.FirstClient.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var labeled = await SendMessageAndReadAsync(setup.FirstClient, saved.ConversationId, Guid.NewGuid(), "تمرین امروز");
        await SendMessageAndReadAsync(setup.FirstClient, saved.ConversationId, Guid.NewGuid(), "بدون برچسب");
        await WriteP2LabelsAsync(setup.FirstClient, labeled.MessageId, ["درس"], null);
        var query = "savedLabel=" + Uri.EscapeDataString("درس");
        Assert.Equal(labeled.MessageId, Assert.Single((await setup.FirstClient.GetFromJsonAsync<MessageSearchPageResult>("/api/v1/messages/search?" + query, JsonOptions))!.Items).MessageId);
        Assert.Empty((await setup.SecondClient.GetFromJsonAsync<MessageSearchPageResult>("/api/v1/messages/search?" + query, JsonOptions))!.Items);
        Assert.Empty((await setup.FirstClient.GetFromJsonAsync<MessageSearchPageResult>($"/api/v1/messages/search?{query}&conversationId={setup.ConversationId}", JsonOptions))!.Items);
    }

    [Fact]
    public async Task P2PresentationConcurrentLabelReplacementHasOneWinnerWithoutLostUpdate()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        var saved = (await client.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var message = await SendMessageAndReadAsync(client, saved.ConversationId, Guid.NewGuid(), "label race");
        var first = await WriteP2LabelsAsync(client, message.MessageId, ["first"], null);
        var responses = await Task.WhenAll(new[] { "one", "two" }.Select(label => client.PutAsJsonAsync(P2LabelRoute(message.MessageId),
            new ReplaceSavedLabelsCommand([label], first.Version), JsonOptions)));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.PreconditionFailed); }
        finally { foreach (var response in responses) response.Dispose(); }
        Assert.Contains(Assert.Single((await ReadP2LabelsAsync(client, message.MessageId)).Labels), new[] { "ONE", "TWO" });
    }

    [Fact]
    public async Task P2PresentationSavedForwardAfterSourceRemovalDoesNotExposeSourceMetadata()
    {
        using var ownerClient = factory.CreateClient(); using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var member = await CreateMessagingUserAsync(memberClient);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        const string sourceTitle = "عنوان محرمانهٔ منبع";
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", PersonalCommand(sourceTitle, member.UserId));
        var original = await SendMessageAndReadAsync(ownerClient, group.ConversationId, Guid.NewGuid(), "نسخهٔ مستقل مجاز");
        var saved = (await memberClient.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var forwarded = await memberClient.PostAsJsonAsync($"/api/v1/conversations/{group.ConversationId}/messages/{original.MessageId}/forward",
            new ForwardMessageCommand(saved.ConversationId, Guid.NewGuid()), JsonOptions);
        forwarded.EnsureSuccessStatusCode(); var copy = (await forwarded.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
        await WriteP2LabelsAsync(memberClient, copy.MessageId, ["درس"], null);
        (await ownerClient.DeleteAsync($"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await memberClient.GetAsync($"/api/v1/conversations/{group.ConversationId}/messages")).StatusCode);
        var response = await memberClient.GetAsync($"/api/v1/conversations/{saved.ConversationId}/messages"); response.EnsureSuccessStatusCode();
        var wire = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(sourceTitle, wire); Assert.DoesNotContain(group.ConversationId.ToString(), wire); Assert.DoesNotContain(owner.UserId.ToString(), wire);
        var copied = Assert.Single((await response.Content.ReadFromJsonAsync<ConversationMessagePageResult>(JsonOptions))!.Messages);
        Assert.Equal("نسخهٔ مستقل مجاز", copied.Text); Assert.True(copied.IsForwarded); Assert.Single((await ReadP2LabelsAsync(memberClient, copy.MessageId)).Labels);
    }

    [Fact]
    public async Task P2PresentationFolderCrudIsOwnerOnlyAndDeletionKeepsConversation()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var id = Guid.NewGuid();
        var created = await WriteP2FolderAsync(setup.FirstClient, id, P2Folder("مدرسه", [new(setup.ConversationId, true)]));
        Assert.Single((await setup.FirstClient.GetFromJsonAsync<ChatFolderResult[]>("/api/v1/messaging/folders", JsonOptions))!);
        Assert.Empty((await setup.SecondClient.GetFromJsonAsync<ChatFolderResult[]>("/api/v1/messaging/folders", JsonOptions))!);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.PutAsJsonAsync($"/api/v1/messaging/folders/{id}", P2Folder("دستکاری") with { ExpectedVersion = created.Version }, JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.GetAsync($"/api/v1/conversations?folderId={id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.DeleteAsync($"/api/v1/messaging/folders/{id}?expectedVersion={Uri.EscapeDataString(created.Version)}")).StatusCode);
        var updated = await WriteP2FolderAsync(setup.FirstClient, id, P2Folder("کار") with { ExpectedVersion = created.Version });
        Assert.NotEqual(created.Version, updated.Version);
        (await setup.FirstClient.DeleteAsync($"/api/v1/messaging/folders/{id}?expectedVersion={Uri.EscapeDataString(updated.Version)}")).EnsureSuccessStatusCode();
        Assert.Empty((await setup.FirstClient.GetFromJsonAsync<ChatFolderResult[]>("/api/v1/messaging/folders", JsonOptions))!);
        Assert.Equal(HttpStatusCode.OK, (await setup.FirstClient.GetAsync($"/api/v1/conversations/{setup.ConversationId}/messages")).StatusCode);
    }

    [Fact]
    public async Task P2PresentationFolderOverrideDoesNotGrantAccessOrKeepRemovedGroupVisible()
    {
        using var ownerClient = factory.CreateClient(); using var memberClient = factory.CreateClient(); using var outsider = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient); var member = await CreateMessagingUserAsync(memberClient); await CreateMessagingUserAsync(outsider);
        await factory.AllowPersonalGroupInviteAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(ownerClient, "groups", PersonalCommand("دسترسی پوشه", member.UserId));
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsJsonAsync($"/api/v1/messaging/folders/{id}", P2Folder("غیرمجاز", [new(group.ConversationId, true)]), JsonOptions)).StatusCode);
        var folder = await WriteP2FolderAsync(memberClient, id, P2Folder("مجاز", [new(group.ConversationId, true)]) with { IncludedTypes = FolderConversationTypes.None });
        Assert.Equal(group.ConversationId, Assert.Single(await ReadP2FolderConversationsAsync(memberClient, id)).ConversationId);
        (await ownerClient.DeleteAsync($"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}")).EnsureSuccessStatusCode();
        Assert.Empty(await ReadP2FolderConversationsAsync(memberClient, id));
        Assert.Equal(HttpStatusCode.NotFound, (await memberClient.PutAsJsonAsync($"/api/v1/messaging/folders/{id}",
            P2Folder("تلاش دوباره", [new(group.ConversationId, true)]) with { ExpectedVersion = folder.Version }, JsonOptions)).StatusCode);
    }

    [Fact]
    public async Task P2PresentationFolderFiltersOverridesAndPinOrderingApplyBeforePagination()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        var groups = new List<Guid>(); for (var index = 0; index < 7; index++) groups.Add((await CreateManagedAndReadAsync(client, "groups", PersonalCommand("پوشه " + index))).ConversationId);
        var channel = await CreateManagedAndReadAsync(client, "channels", PersonalCommand("کانال خارج فیلتر"));
        var id = Guid.NewGuid();
        var folder = await WriteP2FolderAsync(client, id, P2Folder("گروه‌ها", [new(groups[0], true, 0), new(groups[1], false)]) with { IncludedTypes = FolderConversationTypes.Group });
        var page1 = await ReadP2FolderConversationsAsync(client, id, 1, 3); var page2 = await ReadP2FolderConversationsAsync(client, id, 2, 3);
        Assert.Equal(groups[0], page1[0].ConversationId); Assert.Equal(6, page1.Concat(page2).Select(c => c.ConversationId).Distinct().Count());
        Assert.DoesNotContain(page1.Concat(page2), c => c.ConversationId == groups[1] || c.ConversationId == channel.ConversationId);
        await WriteP2FolderAsync(client, id, P2Folder("استثنا", [new(channel.ConversationId, true)]) with {
            IncludedTypes = FolderConversationTypes.Group, ExcludedTypes = FolderConversationTypes.Group, ExpectedVersion = folder.Version });
        Assert.Equal(channel.ConversationId, Assert.Single(await ReadP2FolderConversationsAsync(client, id)).ConversationId);
    }

    [Fact]
    public async Task P2PresentationFolderRespectsUnreadAndArchiveWithoutChangingMembership()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var id = Guid.NewGuid();
        await WriteP2FolderAsync(setup.SecondClient, id, P2Folder("خوانده‌نشده") with { UnreadOnly = true });
        Assert.Empty(await ReadP2FolderConversationsAsync(setup.SecondClient, id));
        var sent = await SendMessageAndReadAsync(setup.FirstClient, setup.ConversationId, Guid.NewGuid(), "unread folder");
        Assert.Equal(setup.ConversationId, Assert.Single(await ReadP2FolderConversationsAsync(setup.SecondClient, id)).ConversationId);
        (await setup.SecondClient.PostAsync($"/api/v1/conversations/{setup.ConversationId}/archive", null)).EnsureSuccessStatusCode();
        Assert.Empty(await ReadP2FolderConversationsAsync(setup.SecondClient, id));
        var archived = (await setup.SecondClient.GetFromJsonAsync<ConversationSummaryResult[]>($"/api/v1/conversations?filter=Archived&folderId={id}", JsonOptions))!;
        Assert.Equal(setup.ConversationId, Assert.Single(archived).ConversationId);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", sent.Sequence);
        Assert.Empty((await setup.SecondClient.GetFromJsonAsync<ConversationSummaryResult[]>($"/api/v1/conversations?filter=Archived&folderId={id}", JsonOptions))!);
    }

    [Fact]
    public async Task P2PresentationFolderTwoDeviceConcurrentUpdateHasOneWinner()
    {
        using var client = factory.CreateClient(); using var device = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await AuthenticateP1SessionAsync(device, user.PhoneNumber, factory.Services);
        var id = Guid.NewGuid(); var original = await WriteP2FolderAsync(client, id, P2Folder("اول"));
        var responses = await Task.WhenAll(client.PutAsJsonAsync($"/api/v1/messaging/folders/{id}", P2Folder("دستگاه اول") with { ExpectedVersion = original.Version }, JsonOptions),
            device.PutAsJsonAsync($"/api/v1/messaging/folders/{id}", P2Folder("دستگاه دوم") with { ExpectedVersion = original.Version }, JsonOptions));
        try { Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.PreconditionFailed); }
        finally { foreach (var response in responses) response.Dispose(); }
        var one = Assert.Single((await client.GetFromJsonAsync<ChatFolderResult[]>("/api/v1/messaging/folders", JsonOptions))!);
        var two = Assert.Single((await device.GetFromJsonAsync<ChatFolderResult[]>("/api/v1/messaging/folders", JsonOptions))!);
        Assert.Equal(one.Name, two.Name); Assert.Equal(one.Version, two.Version); Assert.NotEqual(original.Version, one.Version);
    }

    [Fact]
    public async Task P2PresentationInvalidNamesTypesLabelsAndFolderCapsFailSafely()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        foreach (var command in new[] { P2Folder(" "), P2Folder("x") with { IncludedTypes = (FolderConversationTypes)16 },
            P2Folder("x") with { Overrides = [new(Guid.Empty, true)] }, P2Folder("x") with { ExpectedVersion = "not-a-version" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/messaging/folders/{Guid.NewGuid()}", command, JsonOptions)).StatusCode);
        for (var index = 0; index < ChatFolder.MaximumFoldersPerUser; index++) await WriteP2FolderAsync(client, Guid.NewGuid(), P2Folder("پوشه " + index));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/messaging/folders/{Guid.NewGuid()}", P2Folder("بیش از سقف"), JsonOptions)).StatusCode);
        var saved = (await client.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        var message = await SendMessageAndReadAsync(client, saved.ConversationId, Guid.NewGuid(), "invalid labels");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(P2LabelRoute(message.MessageId), new ReplaceSavedLabelsCommand([new string('x', 33)], null), JsonOptions)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/messages/search?savedLabel=" + new string('x', 33))).StatusCode);
    }

    [Fact]
    public async Task P2PresentationBoundedFolderSmokeReturnsAllAuthorizedConversationsInStablePages()
    {
        using var client = factory.CreateClient(); await CreateMessagingUserAsync(client);
        var ids = new List<Guid>(); for (var index = 0; index < 25; index++) ids.Add((await CreateManagedAndReadAsync(client, "groups", PersonalCommand("smoke " + index))).ConversationId);
        var id = Guid.NewGuid(); var watch = Stopwatch.StartNew();
        await WriteP2FolderAsync(client, id, P2Folder("بار محلی", ids.Select((c, index) => new FolderOverrideCommand(c, true, index)).ToArray()) with { IncludedTypes = FolderConversationTypes.None });
        var returned = new List<Guid>(); for (var page = 1; page <= 5; page++) returned.AddRange((await ReadP2FolderConversationsAsync(client, id, page, 5)).Select(c => c.ConversationId));
        Assert.Equal(ids, returned); Assert.Equal(25, returned.Distinct().Count());
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), "این فقط smoke محلی محدود است، نه ظرفیت Production.");
    }

    private static string P2LabelRoute(Guid id) => $"/api/v1/conversations/saved/messages/{id}/labels";
    private static SaveChatFolderCommand P2Folder(string name, IReadOnlyCollection<FolderOverrideCommand>? overrides = null) =>
        new(name, 0, FolderConversationTypes.All, FolderConversationTypes.None, false, overrides ?? [], null);
    private static async Task<ChatFolderResult> WriteP2FolderAsync(HttpClient client, Guid id, SaveChatFolderCommand command)
    {
        using var response = await client.PutAsJsonAsync($"/api/v1/messaging/folders/{id}", command, JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ChatFolderResult>(JsonOptions))!;
    }
    private static async Task<SavedMessageLabelsResult> WriteP2LabelsAsync(HttpClient client, Guid id, string[] labels, string? version)
    {
        using var response = await client.PutAsJsonAsync(P2LabelRoute(id), new ReplaceSavedLabelsCommand(labels, version), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<SavedMessageLabelsResult>(JsonOptions))!;
    }
    private static async Task<SavedMessageLabelsResult> ReadP2LabelsAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<SavedMessageLabelsResult>(P2LabelRoute(id), JsonOptions))!;
    private static async Task<ConversationSummaryResult[]> ReadP2FolderConversationsAsync(HttpClient client, Guid id, int page = 1, int size = 30) =>
        (await client.GetFromJsonAsync<ConversationSummaryResult[]>($"/api/v1/conversations?folderId={id}&page={page}&pageSize={size}", JsonOptions))!;
}
