using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData(ConversationChecklistMode.Personal)] [InlineData(ConversationChecklistMode.Collaborative)]
    public async Task P2ChecklistForwardIsIndependentTextWithoutAnyStateIdentityVersionOrInteractiveRow(ConversationChecklistMode mode)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(mode));
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        checklist = await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        var saved = (await setup.FirstClient.GetFromJsonAsync<SavedConversationResult>("/api/v1/conversations/saved", JsonOptions))!;
        using var response = await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/forward", new ForwardMessageCommand(saved.ConversationId, Guid.NewGuid()), JsonOptions);
        response.EnsureSuccessStatusCode(); var forwarded = (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
        Assert.Equal(MessageKind.Text, forwarded.Kind); var snapshot = Assert.Single(await factory.GetMessagesAsync(saved.ConversationId));
        Assert.Equal(checklist.Title + "\n1. " + checklist.Items.First().Text + "\n2. " + checklist.Items.Last().Text, snapshot.Text);
        Assert.DoesNotContain(checklist.Version, snapshot.Text!); Assert.DoesNotContain(setup.First.UserId.ToString(), snapshot.Text!);
        Assert.All(checklist.Items, i => Assert.DoesNotContain(i.ItemId.ToString(), snapshot.Text!));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.ConversationChecklists.AnyAsync(c => c.Id == forwarded.MessageId)); Assert.False(await db.ConversationChecklistPersonalStates.AnyAsync(s => s.ChecklistId == forwarded.MessageId));
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.GetAsync(P2ChecklistRoute(saved.ConversationId, forwarded.MessageId))).StatusCode);
        await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, false);
        Assert.Equal(snapshot.Text, Assert.Single(await factory.GetMessagesAsync(saved.ConversationId)).Text);
    }

    [Fact]
    public async Task P2ChecklistDeleteForMeIsPrivateAndDeleteForEveryoneRetainsHistoryButConcealsAllDetailsAndDelta()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        checklist = await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        using var unauthorized = await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(receipt.Version)}");
        Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
        (await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/for-me")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.SecondClient.GetAsync(P2ChecklistRoute(setup.ConversationId, receipt.MessageId))).StatusCode);
        Assert.True((await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId)).Items.First().IsChecked);
        Assert.DoesNotContain((await GetChangesAsync(setup.SecondClient, setup.ConversationId)).Changes, c => c.Type == MessagingChangeType.ChecklistChanged);
        (await setup.FirstClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/for-everyone?expectedVersion={Uri.EscapeDataString(receipt.Version)}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.FirstClient.GetAsync(P2ChecklistRoute(setup.ConversationId, receipt.MessageId))).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(setup.FirstClient, setup.ConversationId)).Changes, c => c.Type == MessagingChangeType.ChecklistChanged);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.True(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationChecklistPersonalStates.AnyAsync(s => s.ChecklistId == receipt.MessageId));
    }

    [Fact]
    public async Task P2ChecklistUnavailableReplySearchAndGenericEditNeverLeakItemsOrPersonalState()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        var reply = await SendAdvancedMessageAsync(setup.SecondClient, setup.ConversationId, new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پاسخ امن", ReplyToMessageId = receipt.MessageId });
        var search = $"/api/v1/messages/search?q={Uri.EscapeDataString(checklist.Title)}&kind=Checklist&conversationId={setup.ConversationId}";
        var searchWire = await setup.SecondClient.GetStringAsync(search); Assert.Contains(checklist.Title, searchWire);
        foreach (var route in new[] { search, $"/api/v1/conversations/{setup.ConversationId}/messages" })
        {
            var wire = await setup.SecondClient.GetStringAsync(route);
            Assert.DoesNotContain("isChecked", wire, StringComparison.OrdinalIgnoreCase); Assert.All(checklist.Items, i => Assert.DoesNotContain(i.Text, wire));
        }
        using var genericEdit = await setup.FirstClient.PatchAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}", new EditMessageCommand("دورزدن تعریف", receipt.Version), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, genericEdit.StatusCode); Assert.Equal(MessagingErrorCodes.MessageNotEditable, await ReadProblemCodeAsync(genericEdit));
        (await setup.SecondClient.DeleteAsync($"/api/v1/conversations/{setup.ConversationId}/messages/{receipt.MessageId}/for-me")).EnsureSuccessStatusCode();
        var history = await setup.SecondClient.GetStringAsync($"/api/v1/conversations/{setup.ConversationId}/messages");
        Assert.DoesNotContain(checklist.Title, history); Assert.All(checklist.Items, i => Assert.DoesNotContain(i.Text, history));
        Assert.DoesNotContain(checklist.Title, await setup.SecondClient.GetStringAsync(search));
        using var stranger = factory.CreateClient(); await CreateMessagingUserAsync(stranger); Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(P2ChecklistRoute(setup.ConversationId, receipt.MessageId))).StatusCode);
        Assert.DoesNotContain(checklist.Title, await stranger.GetStringAsync($"/api/v1/messages/search?q={Uri.EscapeDataString(checklist.Title)}"));
        Assert.NotEqual(Guid.Empty, reply.MessageId);
    }

    [Fact]
    public async Task P2ChecklistFromJoinAndClearHistoryHideOldDefinitionItemsAndChanges()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: false); var id = setup.Conversation.ConversationId;
        (await setup.OwnerClient.PatchAsJsonAsync($"/api/v1/conversations/{id}/settings", new UpdateConversationSettingsCommand(ConversationVisibility.Private, GroupHistoryPolicy.FromJoin,
            GroupMemberCapability.LegacyMemberDefault, setup.Conversation.Version), JsonOptions)).EnsureSuccessStatusCode();
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id); var checklist = await ReadP2ChecklistAsync(setup.OwnerClient, id, receipt.MessageId);
        await SetP2ChecklistAsync(setup.OwnerClient, checklist, checklist.Items.First().ItemId, true);
        (await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{id}/members", new AddConversationMemberCommand(setup.Target.UserId), JsonOptions)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.TargetClient.GetAsync(P2ChecklistRoute(id, receipt.MessageId))).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(setup.TargetClient, id)).Changes, c => c.ResourceId == receipt.MessageId);
        (await setup.OwnerClient.PostAsync($"/api/v1/conversations/{id}/clear-history", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await setup.OwnerClient.GetAsync(P2ChecklistRoute(id, receipt.MessageId))).StatusCode);
        Assert.DoesNotContain((await GetChangesAsync(setup.OwnerClient, id)).Changes, c => c.ResourceId == receipt.MessageId && c.Type == MessagingChangeType.ChecklistChanged);
    }
}
