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
    private static CreateConversationChecklistCommand P2Checklist(ConversationChecklistMode mode = ConversationChecklistMode.Personal) =>
        new(Guid.NewGuid(), "فهرست کارهای نمونه", mode, [new(Guid.NewGuid(), "کار اول"), new(Guid.NewGuid(), "کار دوم")]);
    private static string P2ChecklistRoute(Guid conversationId, Guid messageId) => $"/api/v1/conversations/{conversationId}/messages/{messageId}/checklist";
    private static async Task<MessageReceiptResult> CreateP2ChecklistAsync(HttpClient client, Guid conversationId, CreateConversationChecklistCommand? command = null)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/checklists", command ?? P2Checklist(), JsonOptions);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
    }
    private static async Task<ConversationChecklistResult> ReadP2ChecklistAsync(HttpClient client, Guid conversationId, Guid messageId) =>
        (await client.GetFromJsonAsync<ConversationChecklistResult>(P2ChecklistRoute(conversationId, messageId), JsonOptions))!;
    private static Task<HttpResponseMessage> SetP2ChecklistResponseAsync(HttpClient client, ConversationChecklistResult checklist, Guid itemId, bool isChecked,
        string? stateVersion = null, string? definitionVersion = null) => client.PutAsJsonAsync(
            P2ChecklistRoute(checklist.ConversationId, checklist.MessageId) + $"/items/{itemId}",
            new SetChecklistItemCommand(isChecked, definitionVersion ?? checklist.Version, stateVersion), JsonOptions);
    private static async Task<ConversationChecklistResult> SetP2ChecklistAsync(HttpClient client, ConversationChecklistResult checklist, Guid itemId, bool isChecked)
    {
        using var response = await SetP2ChecklistResponseAsync(client, checklist, itemId, isChecked, checklist.Items.Single(i => i.ItemId == itemId).StateVersion);
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<ConversationChecklistResult>(JsonOptions))!;
    }
    private static UpdateConversationChecklistCommand P2ChecklistUpdate(ConversationChecklistResult checklist) =>
        new(checklist.Title, checklist.Mode, checklist.Items.Select(i => new ChecklistItemInput(i.ItemId, i.Text)).ToArray(), checklist.Version);
    private static Task<HttpResponseMessage> UpdateP2ChecklistResponseAsync(HttpClient client, ConversationChecklistResult checklist, UpdateConversationChecklistCommand command) =>
        client.PutAsJsonAsync(P2ChecklistRoute(checklist.ConversationId, checklist.MessageId), command, JsonOptions);
    private static async Task<ConversationChecklistResult> UpdateP2ChecklistAsync(HttpClient client, ConversationChecklistResult checklist, UpdateConversationChecklistCommand command)
    {
        using var response = await UpdateP2ChecklistResponseAsync(client, checklist, command); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationChecklistResult>(JsonOptions))!;
    }

    [Theory]
    [InlineData(ConversationChecklistMode.Personal)]
    [InlineData(ConversationChecklistMode.Collaborative)]
    public async Task P2ChecklistCreateHasExplicitModeStableItemsAndIdempotentReceipt(ConversationChecklistMode mode)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Checklist(mode);
        var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, command);
        Assert.Equal(MessageKind.Checklist, receipt.Kind); Assert.Equal(receipt, await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, command));
        var checklist = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId);
        Assert.Equal(mode, checklist.Mode); Assert.Equal(command.Title, checklist.Title); Assert.False(checklist.IsCompleted);
        Assert.Equal(command.Items.Select(i => i.ItemId), checklist.Items.Select(i => i.ItemId)); Assert.All(checklist.Items, i => Assert.False(i.IsChecked));
        Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        Assert.Equal(HttpStatusCode.Conflict, (await setup.FirstClient.PostAsJsonAsync($"/api/v1/conversations/{setup.ConversationId}/checklists", command with { Title = "متفاوت" }, JsonOptions)).StatusCode);
        await factory.DrainNotificationOutboxAsync(); Assert.Equal(1, await factory.CountNotificationsAsync(receipt.MessageId, setup.Second.UserId));
    }

    [Fact]
    public async Task P2ChecklistPersonalStateAndDerivedCompletionArePrivateAndConvergeAcrossSessions()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var first = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        var second = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId);
        foreach (var item in first.Items.ToArray()) first = await SetP2ChecklistAsync(setup.FirstClient, first, item.ItemId, true);
        Assert.True(first.IsCompleted); Assert.Equal(second.Version, first.Version); Assert.Equal(second.MessageVersion, first.MessageVersion);
        var privateResult = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId);
        Assert.Equal(second, privateResult with { Items = second.Items }); Assert.False(privateResult.IsCompleted);
        Assert.All(privateResult.Items, i => { Assert.False(i.IsChecked); Assert.Null(i.StateVersion); });
        using var device = factory.CreateClient(); await AuthenticateP1SessionAsync(device, setup.First.PhoneNumber, factory.Services);
        var recovered = await ReadP2ChecklistAsync(device, setup.ConversationId, receipt.MessageId);
        Assert.True(recovered.IsCompleted); Assert.Equal(first.Items, recovered.Items);
        recovered = await SetP2ChecklistAsync(device, recovered, recovered.Items.First().ItemId, false);
        Assert.False(recovered.IsCompleted); Assert.False((await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId)).Items.First().IsChecked);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.All(await db.ConversationChecklistPersonalStates.Where(s => s.ChecklistId == receipt.MessageId).ToArrayAsync(), s => Assert.Equal(setup.First.UserId, s.UserId));
        Assert.All(await db.ConversationChecklistItems.Where(i => i.ChecklistId == receipt.MessageId).ToArrayAsync(), i => Assert.False(i.IsChecked));
    }

    [Fact]
    public async Task P2ChecklistCollaborativeCheckUncheckAndCompletionUseOneSharedState()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(ConversationChecklistMode.Collaborative));
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        foreach (var item in checklist.Items.ToArray()) checklist = await SetP2ChecklistAsync(setup.FirstClient, checklist, item.ItemId, true);
        var peer = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId);
        Assert.True(peer.IsCompleted); Assert.Equal(checklist.Items, peer.Items);
        peer = await SetP2ChecklistAsync(setup.SecondClient, peer, peer.Items.First().ItemId, false); Assert.False(peer.IsCompleted);
        Assert.False((await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId)).Items.First().IsChecked);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationChecklistPersonalStates.AnyAsync(s => s.ChecklistId == receipt.MessageId));
    }

    [Theory]
    [InlineData(ConversationChecklistMode.Personal)] [InlineData(ConversationChecklistMode.Collaborative)]
    public async Task P2ChecklistEquivalentCheckAndUncheckHaveNoDuplicateEventOrVersion(ConversationChecklistMode mode)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(mode));
        var initial = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId); var item = initial.Items.First().ItemId;
        var before = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        var noOp = await SetP2ChecklistAsync(setup.SecondClient, initial, item, false); Assert.Equal(initial.Items, noOp.Items);
        Assert.Empty((await GetChangesAsync(setup.SecondClient, setup.ConversationId, before.NextCursor)).Changes);
        var checkedState = await SetP2ChecklistAsync(setup.SecondClient, initial, item, true);
        var retry = await SetP2ChecklistAsync(setup.SecondClient, initial, item, true); Assert.Equal(checkedState.Items, retry.Items);
        var uncheckedState = await SetP2ChecklistAsync(setup.SecondClient, checkedState, item, false);
        retry = await SetP2ChecklistAsync(setup.SecondClient, checkedState, item, false); Assert.Equal(uncheckedState.Items, retry.Items);
        Assert.Equal(2, (await GetChangesAsync(setup.SecondClient, setup.ConversationId, before.NextCursor)).Changes.Count(c => c.Type == MessagingChangeType.ChecklistChanged));
        using var stale = await SetP2ChecklistResponseAsync(setup.SecondClient, initial, item, true, initial.Items.First().StateVersion);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode); Assert.Equal(MessagingErrorCodes.ChecklistStateConflict, await ReadProblemCodeAsync(stale));
        await factory.DrainNotificationOutboxAsync(); Assert.Equal(1, await factory.CountNotificationsAsync(receipt.MessageId, setup.Second.UserId));
    }

    [Theory]
    [InlineData(ConversationChecklistMode.Personal)] [InlineData(ConversationChecklistMode.Collaborative)]
    public async Task P2ChecklistVersionedReplacementRetainsHistoryWithoutReusingMeaningOrExposingPrivateActivity(ConversationChecklistMode mode)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var command = P2Checklist(mode); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, command);
        var original = await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId); var retired = original.Items.First();
        await SetP2ChecklistAsync(setup.SecondClient, original, retired.ItemId, true);
        var owner = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId); var update = P2ChecklistUpdate(owner);
        using var rewrite = await UpdateP2ChecklistResponseAsync(setup.FirstClient, owner, update with { Items = [new(retired.ItemId, "معنای دیگر")] });
        Assert.Equal(HttpStatusCode.Conflict, rewrite.StatusCode);
        using var changeMode = await UpdateP2ChecklistResponseAsync(setup.FirstClient, owner, update with { Mode = mode == ConversationChecklistMode.Personal ? ConversationChecklistMode.Collaborative : ConversationChecklistMode.Personal });
        Assert.Equal(HttpStatusCode.Conflict, changeMode.StatusCode);
        var newItem = new ChecklistItemInput(Guid.NewGuid(), "جایگزین مستقل");
        var changed = await UpdateP2ChecklistAsync(setup.FirstClient, owner, update with { Title = "عنوان نسخه دوم", Items = [update.Items.Last(), newItem] });
        Assert.NotEqual(owner.Version, changed.Version); Assert.NotEqual(owner.MessageVersion, changed.MessageVersion); Assert.False(changed.IsCompleted);
        Assert.DoesNotContain(changed.Items, i => i.ItemId == retired.ItemId); Assert.False(changed.Items.Single(i => i.ItemId == newItem.ItemId).IsChecked);
        using var stale = await UpdateP2ChecklistResponseAsync(setup.FirstClient, owner, update); Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        using var staleTick = await SetP2ChecklistResponseAsync(setup.SecondClient, original, retired.ItemId, false, original.Items.First().StateVersion); Assert.Equal(HttpStatusCode.PreconditionFailed, staleTick.StatusCode);
        using var resurrect = await UpdateP2ChecklistResponseAsync(setup.FirstClient, changed, P2ChecklistUpdate(changed) with { Items = [new(retired.ItemId, retired.Text)] }); Assert.Equal(HttpStatusCode.Conflict, resurrect.StatusCode);
        Assert.Equal(receipt.MessageId, (await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, command)).MessageId);
        Assert.Equal(new[] { command.Title, changed.Title }, await factory.GetMessageRevisionTextsAsync(receipt.MessageId));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var preserved = await db.ConversationChecklistItems.SingleAsync(i => i.ChecklistId == receipt.MessageId && i.Id == retired.ItemId);
        Assert.Equal(2, preserved.RetiredAtRevision); Assert.Equal(retired.Text, preserved.Text);
        if (mode == ConversationChecklistMode.Personal) Assert.True((await db.ConversationChecklistPersonalStates.SingleAsync(s => s.ChecklistId == receipt.MessageId)).IsChecked);
        else Assert.True(preserved.IsChecked);
        Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.Action == "messaging.checklist.definition.changed" && a.TargetId == receipt.MessageId.ToString("D")));
    }

    [Fact]
    public async Task P2ChecklistAddingAndReorderingItemsAreVersionSafeAndPreserveOwnState()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
        checklist = await SetP2ChecklistAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, true);
        var items = P2ChecklistUpdate(checklist).Items.Reverse().Append(new(Guid.NewGuid(), "کار افزوده")).ToArray();
        var changed = await UpdateP2ChecklistAsync(setup.FirstClient, checklist, P2ChecklistUpdate(checklist) with { Items = items });
        Assert.Equal(items.Select(i => i.ItemId), changed.Items.Select(i => i.ItemId)); Assert.True(changed.Items.Single(i => i.ItemId == checklist.Items.First().ItemId).IsChecked);
        using var stale = await SetP2ChecklistResponseAsync(setup.FirstClient, checklist, checklist.Items.First().ItemId, false, checklist.Items.First().StateVersion);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
    }

    [Fact]
    public async Task P2ChecklistNeverCreatesPollQuizAssessmentOrGradeArtifacts()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var before = (await db.ExamAttempts.CountAsync(), await db.ExamGradeRevisions.CountAsync(), await db.ExamGradeReleases.CountAsync(), await db.ConversationPolls.CountAsync(), await db.ConversationQuizAnswers.CountAsync());
        var assessmentBefore = await ReadChecklistAssessmentCountsAsync(db);
        foreach (var mode in Enum.GetValues<ConversationChecklistMode>())
        {
            var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId, P2Checklist(mode));
            var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId);
            foreach (var item in checklist.Items.ToArray()) checklist = await SetP2ChecklistAsync(setup.FirstClient, checklist, item.ItemId, true);
            Assert.True(checklist.IsCompleted);
        }
        Assert.Equal(before, (await db.ExamAttempts.CountAsync(), await db.ExamGradeRevisions.CountAsync(), await db.ExamGradeReleases.CountAsync(), await db.ConversationPolls.CountAsync(), await db.ConversationQuizAnswers.CountAsync()));
        Assert.Equal(assessmentBefore, await ReadChecklistAssessmentCountsAsync(db));
    }

    private static async Task<long[]> ReadChecklistAssessmentCountsAsync(MakanDbContext db)
    {
        var tables = db.Model.GetEntityTypes().Where(e => e.GetSchema() == "assessment").Select(e => e.GetTableName()!).Distinct().Order().ToArray();
        Assert.NotEmpty(tables); var counts = new List<long>();
        foreach (var table in tables)
        {
            // نام جدول از metadata ثابت مدل است، نه ورودی HTTP؛ تنها count و بدون دادهٔ آموزشی خوانده می‌شود.
            var result = await db.Database.SqlQuery<long>($"""
                DECLARE @table sysname = {table};
                DECLARE @sql nvarchar(max) = N'SELECT COUNT_BIG(*) AS [Value] FROM [assessment].' + QUOTENAME(@table);
                EXEC sys.sp_executesql @sql;
                """).ToArrayAsync();
            counts.Add(Assert.Single(result));
        }
        return counts.ToArray();
    }

    [Fact]
    public async Task P2ChecklistClientSuppliedUserIdCannotSelectOrOverwriteAnotherUsersPersonalState()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var receipt = await CreateP2ChecklistAsync(setup.FirstClient, setup.ConversationId);
        var checklist = await ReadP2ChecklistAsync(setup.FirstClient, setup.ConversationId, receipt.MessageId); var item = checklist.Items.First();
        using var response = await setup.FirstClient.PutAsJsonAsync(P2ChecklistRoute(setup.ConversationId, receipt.MessageId) + $"/items/{item.ItemId}",
            new { UserId = setup.Second.UserId, IsChecked = true, ExpectedDefinitionVersion = checklist.Version, ExpectedStateVersion = item.StateVersion }, JsonOptions);
        response.EnsureSuccessStatusCode();
        var self = (await setup.FirstClient.GetFromJsonAsync<ConversationChecklistResult>(P2ChecklistRoute(setup.ConversationId, receipt.MessageId) + $"?userId={setup.Second.UserId}", JsonOptions))!;
        Assert.True(self.Items.First().IsChecked); Assert.All((await ReadP2ChecklistAsync(setup.SecondClient, setup.ConversationId, receipt.MessageId)).Items, i => Assert.False(i.IsChecked));
        await using var scope = factory.Services.CreateAsyncScope(); Assert.Equal(setup.First.UserId,
            (await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationChecklistPersonalStates.SingleAsync(s => s.ChecklistId == receipt.MessageId)).UserId);
    }
}
