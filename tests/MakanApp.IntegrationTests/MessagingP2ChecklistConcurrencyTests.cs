using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2ChecklistConcurrentCreationHasOneMessageDefinitionAndNotification()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Checklist();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<MessageReceiptResult> Create() { await gate.Task; return await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, command); }
        var first = Create(); var second = Create(); gate.SetResult(); await Task.WhenAll(first, second); Assert.Equal(await first, await second);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Single(await db.ConversationChecklists.Where(c => c.ConversationId == setup.ConversationId).ToArrayAsync());
        var receipt = await first;
        Assert.Equal(2, await db.ConversationChecklistItems.CountAsync(i => i.ChecklistId == receipt.MessageId));
        await factory.DrainNotificationOutboxAsync(); Assert.Equal(1, await factory.CountNotificationsAsync((await first).MessageId, setup.Second.UserId));
    }

    [Theory]
    [InlineData(ConversationChecklistMode.Personal)] [InlineData(ConversationChecklistMode.Collaborative)]
    public async Task P2ChecklistConcurrentTwoDeviceDoubleCheckHasOneSqlEffect(ConversationChecklistMode mode)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(mode));
        using var device = factory.CreateClient(); await AuthenticateP1SessionAsync(device, setup.Second.PhoneNumber, factory.Services);
        var checklist = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId); var item = checklist.Items.First().ItemId;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ConversationChecklistResult> Check(HttpClient client) { await gate.Task; return await SetP2ChecklistAsync(client, checklist, item, true); }
        var first = Check(setup.SecondClient); var second = Check(device); gate.SetResult(); await Task.WhenAll(first, second);
        Assert.Equal((await first).Items, (await second).Items);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Single(await db.MessagingChangeEvents.Where(c => c.ResourceId == receipt.MessageId && c.ChangeType == MessagingChangeType.ChecklistChanged).ToArrayAsync());
        if (mode == ConversationChecklistMode.Personal) Assert.Equal(1, (await db.ConversationChecklistPersonalStates.SingleAsync(s => s.ChecklistId == receipt.MessageId)).Revision);
        else Assert.Equal(2, (await db.ConversationChecklistItems.SingleAsync(i => i.ChecklistId == receipt.MessageId && i.Id == item)).StateRevision);
    }

    [Theory]
    [InlineData(ConversationChecklistMode.Personal)] [InlineData(ConversationChecklistMode.Collaborative)]
    public async Task P2ChecklistCheckVersusUncheckCannotSilentlyRestoreStaleState(ConversationChecklistMode mode)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(mode));
        var initial = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId); var item = initial.Items.First().ItemId;
        initial = await SetP2ChecklistAsync(setup.SecondClient, initial, item, true);
        using var device = factory.CreateClient(); await AuthenticateP1SessionAsync(device, mode == ConversationChecklistMode.Personal ? setup.Second.PhoneNumber : setup.First.PhoneNumber, factory.Services);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Mutate(HttpClient client, bool value) { await gate.Task; return await SetP2ChecklistResponseAsync(client, initial, item, value, initial.Items.First().StateVersion); }
        var uncheck = Mutate(setup.SecondClient, false); var check = Mutate(device, true); gate.SetResult(); await Task.WhenAll(uncheck, check);
        using var uncheckedResponse = await uncheck; using var checkedResponse = await check;
        Assert.Equal(HttpStatusCode.OK, uncheckedResponse.StatusCode); Assert.Contains(checkedResponse.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.PreconditionFailed });
        Assert.False((await ReadP2ChecklistAsync(device, setup.ConversationId, receipt.MessageId)).Items.First().IsChecked);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(2, await db.MessagingChangeEvents.CountAsync(c => c.ResourceId == receipt.MessageId && c.ChangeType == MessagingChangeType.ChecklistChanged));
    }

    [Theory]
    [InlineData(ConversationChecklistMode.Personal)] [InlineData(ConversationChecklistMode.Collaborative)]
    public async Task P2ChecklistDefinitionRetirementRacingInteractionNeverOrphansOrReattributesState(ConversationChecklistMode mode)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(mode));
        var original = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId); var item = original.Items.First();
        var replacement = new ChecklistItemInput(Guid.NewGuid(), "آیتم جایگزین");
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Edit() { await gate.Task; return await UpdateP2ChecklistResponseAsync(setup.FirstClient, original, P2ChecklistUpdate(original) with { Items = [replacement] }); }
        async Task<HttpResponseMessage> Tick() { await gate.Task; return await SetP2ChecklistResponseAsync(setup.SecondClient, original, item.ItemId, true, item.StateVersion); }
        var edit = Edit(); var tick = Tick(); gate.SetResult(); await Task.WhenAll(edit, tick);
        using var editResult = await edit; using var tickResult = await tick; Assert.Equal(HttpStatusCode.OK, editResult.StatusCode);
        Assert.Contains(tickResult.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.PreconditionFailed });
        var current = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId);
        Assert.Equal(replacement.ItemId, Assert.Single(current.Items).ItemId); Assert.False(current.IsCompleted); Assert.False(current.Items.Single().IsChecked);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var old = await db.ConversationChecklistItems.SingleAsync(i => i.ChecklistId == receipt.MessageId && i.Id == item.ItemId);
        Assert.NotNull(old.RetiredAtRevision); Assert.Equal(item.Text, old.Text);
        if (tickResult.StatusCode == HttpStatusCode.OK && mode == ConversationChecklistMode.Personal)
            Assert.Equal(item.ItemId, (await db.ConversationChecklistPersonalStates.SingleAsync(s => s.ChecklistId == receipt.MessageId)).ItemId);
        if (tickResult.StatusCode == HttpStatusCode.OK && mode == ConversationChecklistMode.Collaborative) Assert.True(old.IsChecked);
    }

    [Fact]
    public async Task P2ChecklistRemovalRacingMutationAlwaysRevokesFutureWrites()
    {
        using var setup = await CreateP2ManagedSetupAsync("groups", initiallyMember: true); var id = setup.Conversation.ConversationId;
        var receipt = await CreateP2ChecklistAsync(setup.OwnerClient, id); var checklist = await ReadP2ChecklistAsync(setup.TargetClient, id, receipt.MessageId);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<HttpResponseMessage> Remove() { await gate.Task; return await setup.OwnerClient.DeleteAsync($"/api/v1/conversations/{id}/members/{setup.Target.UserId}"); }
        async Task<HttpResponseMessage> Tick() { await gate.Task; return await SetP2ChecklistResponseAsync(setup.TargetClient, checklist, checklist.Items.First().ItemId, true); }
        var remove = Remove(); var tick = Tick(); gate.SetResult(); await Task.WhenAll(remove, tick);
        using var removal = await remove; using var ticked = await tick; Assert.Equal(HttpStatusCode.OK, removal.StatusCode);
        Assert.Contains(ticked.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        Assert.Equal(HttpStatusCode.NotFound, (await SetP2ChecklistResponseAsync(setup.TargetClient, checklist, checklist.Items.Last().ItemId, true)).StatusCode);
    }

    [Fact]
    public async Task P2ChecklistSqlUniquenessAndModeKeysPreventDuplicateOrCrossModePersonalState()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var item = await db.ConversationChecklistItems.AsNoTracking().SingleAsync(i => i.ChecklistId == receipt.MessageId && i.Id == checklist.Items.First().ItemId);
            db.ConversationChecklistPersonalStates.Add(ConversationChecklistPersonalState.Create(item, setup.First.UserId, true));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); Assert.Contains(Assert.IsType<SqlException>(duplicate.InnerException).Number, new[] { 2601, 2627 });
        }
        var collaborative = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(ConversationChecklistMode.Collaborative));
        var shared = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, collaborative.MessageId); var sharedId = shared.Items.First().ItemId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var invalid = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [messaging].[ConversationChecklistPersonalStates] ([ChecklistId],[ItemId],[UserId],[Mode],[IsChecked],[Revision]) VALUES ({collaborative.MessageId},{sharedId},{setup.First.UserId},1,1,1)"));
            Assert.Equal(547, invalid.Number);
            invalid = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [messaging].[ConversationChecklistItems] SET [IsChecked]=1 WHERE [ChecklistId]={receipt.MessageId}")); Assert.Equal(547, invalid.Number);
            invalid = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [messaging].[Messages] SET [Text]=NULL WHERE [Id]={receipt.MessageId}")); Assert.Equal(547, invalid.Number);
        }
    }
}
