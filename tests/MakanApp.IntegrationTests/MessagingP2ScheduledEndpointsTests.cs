using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task P2ScheduledDoesNotSendEarlyAndDueDispatchCreatesOneMessage()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var command = new ScheduleMessageCommand(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "زمان‌بندی" });
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, command);
            var repeat = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, command); Assert.True(repeat.AlreadyExisted); Assert.Equal(task.Id, repeat.Id);
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            await DispatchP2ScheduledAsync(); Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            Assert.Equal(HttpStatusCode.Conflict, (await SendMessageAsync(setup.FirstClient, setup.ConversationId, command.Message.ClientMessageId, command.Message.Text!)).StatusCode);
            factory.Clock.SetUtcNow(now.AddMinutes(2)); await DispatchP2ScheduledAsync(); await DispatchP2ScheduledAsync();
            var message = Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
            var sent = await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id);
            Assert.Equal(ScheduledMessageStatus.Dispatched, sent.Status); Assert.Equal(message.Id, sent.DispatchedMessageId);
            Assert.Equal(now.AddMinutes(2), message.SentAtUtc);
            Assert.Equal(task.Id, (await ScheduleP2Async(setup.FirstClient, setup.ConversationId, command)).Id);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledConcurrentCreatesAndWorkersDoNotDuplicate()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var command = new ScheduleMessageCommand(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "یک اثر" });
            var created = await Task.WhenAll(ScheduleP2Async(setup.FirstClient, setup.ConversationId, command), ScheduleP2Async(setup.FirstClient, setup.ConversationId, command));
            Assert.Equal(created[0].Id, created[1].Id); factory.Clock.SetUtcNow(now.AddMinutes(2));
            await Task.WhenAll(DispatchP2ScheduledAsync(), DispatchP2ScheduledAsync());
            Assert.Single(await factory.GetMessagesAsync(setup.ConversationId)); Assert.Equal(ScheduledMessageStatus.Dispatched, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, created[0].Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledExpiredClaimIsRecoveredByNewScopeAndOldWorkerCannotOverwriteReceipt()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پس از restart" }));
            factory.Clock.SetUtcNow(now.AddMinutes(2)); ScheduledMessageClaim claim;
            await using (var scope = factory.Services.CreateAsyncScope()) claim = (await scope.ServiceProvider.GetRequiredService<IScheduledMessageStore>().ClaimNextScheduledMessageAsync(CancellationToken.None))!;
            factory.Clock.SetUtcNow(now.AddMinutes(3)); await DispatchP2ScheduledAsync();
            await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<IScheduledMessageStore>().FailScheduledMessageAsync(claim, "OLD_WORKER", false, CancellationToken.None);
            Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
            Assert.Equal(ScheduledMessageStatus.Dispatched, (await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id)).Status);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledReclaimedLeaseRejectsOldWorkerAtSqlSendBoundary()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "lease قدیمی مجوز نیست" }));
            factory.Clock.SetUtcNow(now.AddMinutes(2)); ScheduledMessageClaim old; ScheduledMessageWork work; byte[] hash;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IScheduledMessageStore>(); old = (await store.ClaimNextScheduledMessageAsync(CancellationToken.None))!;
                work = (await store.GetScheduledMessageWorkAsync(old, CancellationToken.None))!;
                hash = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.Where(s => s.Id == task.Id).Select(s => s.PayloadHash).SingleAsync();
            }
            factory.Clock.SetUtcNow(now.AddMinutes(3));
            await using (var scope = factory.Services.CreateAsyncScope()) Assert.NotNull(await scope.ServiceProvider.GetRequiredService<IScheduledMessageStore>().ClaimNextScheduledMessageAsync(CancellationToken.None));
            var authority = work.Authority; var context = new AccessContext(work.UserId, work.UserId, Guid.Empty, authority.WorkspaceType, authority.OrganizationId, authority.MembershipId, authority.Role, authority.SubjectOrganizationPersonId);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var failure = await Assert.ThrowsAsync<MessagingException>(() => scope.ServiceProvider.GetRequiredService<IMessagingStore>().SendMessageAsync(
                    work.UserId, work.ConversationId, context, work.Command with { ScheduledClaim = new(task.Id, old.Token, hash) }, false, 4000, factory.Clock.GetUtcNow().UtcDateTime, CancellationToken.None));
                Assert.Equal(MessagingErrorCodes.ScheduledClaimLost, failure.Code);
            }
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId)); factory.Clock.SetUtcNow(now.AddMinutes(4)); await DispatchP2ScheduledAsync();
            Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledCancelIsPrivateVersionedAndConvergesAcrossSessions()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var secondDevice = factory.CreateClient();
        await AuthenticateP1SessionAsync(secondDevice, setup.First.PhoneNumber, factory.Services);
        var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(DateTime.UtcNow.AddMinutes(10), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "خصوصی" }));
        try
        {
            Assert.Empty((await setup.SecondClient.GetFromJsonAsync<ScheduledMessagePageResult>($"/api/v1/conversations/{setup.ConversationId}/scheduled-messages", JsonOptions))!.Items);
            Assert.Equal(HttpStatusCode.NotFound, (await CancelP2ScheduleAsync(setup.SecondClient, task)).StatusCode);
            var cancelled = await CancelP2ScheduleAsync(secondDevice, task); cancelled.EnsureSuccessStatusCode();
            var fresh = await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id); Assert.Equal(ScheduledMessageStatus.Cancelled, fresh.Status);
            Assert.NotEqual(task.Version, fresh.Version); (await CancelP2ScheduleAsync(setup.FirstClient, task)).EnsureSuccessStatusCode();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task P2ScheduledConcurrentCancelAndDispatchCannotBothTakeEffect(int round)
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = $"رقابت لغو {round}" }));
            factory.Clock.SetUtcNow(now.AddMinutes(2));
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<HttpResponseMessage> CancelWhenStarted() { await start.Task; return await CancelP2ScheduleAsync(setup.FirstClient, task); }
            async Task<int> DispatchWhenStarted() { await start.Task; return await DispatchP2ScheduledAsync(); }
            var cancel = CancelWhenStarted(); var dispatch = DispatchWhenStarted(); start.SetResult();
            await Task.WhenAll(cancel, dispatch); var cancellation = await cancel;
            Assert.Contains(cancellation.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.PreconditionFailed });
            var final = await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, task.Id);
            if (final.Status == ScheduledMessageStatus.Cancelled) Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            else { Assert.Equal(ScheduledMessageStatus.Dispatched, final.Status); Assert.Single(await factory.GetMessagesAsync(setup.ConversationId)); }
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
    [Fact]
    public async Task P2ScheduledRevokedSessionLosesMutationWithoutCreatingFakeWorkerSession()
    {
        using var setup = await CreateAdvancedConversationSetupAsync(); using var survivor = factory.CreateClient();
        await AuthenticateP1SessionAsync(survivor, setup.First.PhoneNumber, factory.Services);
        var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(DateTime.UtcNow.AddMinutes(10), new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "قصد پایدار" }));
        try
        {
            await factory.RevokeSessionByTokenAsync(setup.First.AccessToken);
            Assert.Equal(HttpStatusCode.Unauthorized, (await CancelP2ScheduleAsync(setup.FirstClient, task)).StatusCode);
            (await CancelP2ScheduleAsync(survivor, task)).EnsureSuccessStatusCode();
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); }
    }
    private static async Task<ScheduledMessageResult> ScheduleP2Async(HttpClient client, Guid conversationId, ScheduleMessageCommand command)
    {
        using var response = await client.PostAsJsonAsync($"/api/v1/conversations/{conversationId}/scheduled-messages", command, JsonOptions); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScheduledMessageResult>(JsonOptions))!;
    }
    private static Task<HttpResponseMessage> CancelP2ScheduleAsync(HttpClient client, ScheduledMessageResult task) =>
        client.PostAsJsonAsync($"/api/v1/conversations/{task.ConversationId}/scheduled-messages/{task.Id}/cancel", new CancelScheduledMessageCommand(task.Version), JsonOptions);
    private static async Task<ScheduledMessageResult> ReadP2ScheduleAsync(HttpClient client, Guid conversationId, Guid id) =>
        Assert.Single((await client.GetFromJsonAsync<ScheduledMessagePageResult>($"/api/v1/conversations/{conversationId}/scheduled-messages", JsonOptions))!.Items, s => s.Id == id);
    private async Task<int> DispatchP2ScheduledAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ScheduledMessageProcessor>().DispatchBatchAsync(CancellationToken.None);
    }
    private async Task CleanupP2SchedulesAsync(Guid userId)
    {
        // پاک‌سازی فقط intentهای در انتظارِ actor یکتای همین تست، در SQL ایزوله؛ فایل/پیامی حذف نمی‌شود.
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        foreach (var task in await db.ScheduledMessages.Where(s => s.UserId == userId && s.Status == ScheduledMessageStatus.Scheduled).ToArrayAsync()) task.Cancel(factory.Clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync();
    }
}
