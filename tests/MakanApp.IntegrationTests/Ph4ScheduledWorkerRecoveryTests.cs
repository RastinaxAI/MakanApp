using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task Ph4ScheduledRepairCountsTowardTheSameTwentyItemBatchLimit()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            for (var index = 0; index < ScheduledMessageProcessor.BatchSize + 1; index++)
                await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1),
                    new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = $"آزمون repair محدود {index}" }));
            await using (var scope = factory.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.Where(row => row.ConversationId == setup.ConversationId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AttemptCount, ScheduledMessage.MaximumAttempts)
                        .SetProperty(row => row.ClaimToken, Guid.NewGuid()).SetProperty(row => row.ClaimedUntilUtc, now.AddSeconds(1)));
            factory.Clock.SetUtcNow(now.AddMinutes(2));
            await using (var scope = factory.Services.CreateAsyncScope())
                Assert.Equal(20, await scope.ServiceProvider.GetRequiredService<ScheduledMessageProcessor>().DispatchBatchAsync(default));
            await using (var check = factory.Services.CreateAsyncScope())
            {
                var rows = await check.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.AsNoTracking()
                    .Where(row => row.ConversationId == setup.ConversationId).ToArrayAsync();
                Assert.Equal(20, rows.Count(row => row.Status == ScheduledMessageStatus.Failed));
                Assert.Single(rows, row => row.Status == ScheduledMessageStatus.Scheduled);
            }
            await DispatchP2ScheduledAsync();
            Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
            await using var final = factory.Services.CreateAsyncScope();
            Assert.Equal(21, await final.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.CountAsync(
                row => row.ConversationId == setup.ConversationId && row.Status == ScheduledMessageStatus.Failed));
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task Ph4ScheduledBatchProcessesTwentyOfTwentyOneWithoutDuplicateMessage()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            for (var index = 0; index < ScheduledMessageProcessor.BatchSize + 1; index++)
                await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1),
                    new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = $"آزمون batch {index}" }));
            factory.Clock.SetUtcNow(now.AddMinutes(2));
            await using (var scope = factory.Services.CreateAsyncScope())
                Assert.Equal(20, await scope.ServiceProvider.GetRequiredService<ScheduledMessageProcessor>().DispatchBatchAsync(default));
            Assert.Equal(20, (await factory.GetMessagesAsync(setup.ConversationId)).Length);
            await DispatchP2ScheduledAsync(); await DispatchP2ScheduledAsync();
            Assert.Equal(21, (await factory.GetMessagesAsync(setup.ConversationId)).Length);
            await using var check = factory.Services.CreateAsyncScope();
            var rows = await check.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.AsNoTracking()
                .Where(row => row.ConversationId == setup.ConversationId).ToArrayAsync();
            Assert.All(rows, row => { Assert.Equal(ScheduledMessageStatus.Dispatched, row.Status); Assert.Equal(1, row.AttemptCount); });
            Assert.Equal(21, rows.Select(row => row.DispatchedMessageId).Distinct().Count());
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task Ph4ScheduledExhaustedPoisonDoesNotEndBatchBeforeMultipleHealthyItems()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var poison = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1),
                new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "آخرین claim رهاشده" }));
            for (var index = 0; index < 3; index++)
                await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1).AddSeconds(index + 1),
                    new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = $"سالم {index}" }));
            await using (var scope = factory.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.Where(row => row.Id == poison.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AttemptCount, ScheduledMessage.MaximumAttempts)
                        .SetProperty(row => row.ClaimToken, Guid.NewGuid()).SetProperty(row => row.ClaimedUntilUtc, now.AddSeconds(1)));
            factory.Clock.SetUtcNow(now.AddMinutes(2));
            await DispatchP2ScheduledAsync();
            Assert.Equal(3, (await factory.GetMessagesAsync(setup.ConversationId)).Length);
            var terminal = await ReadP2ScheduleAsync(setup.FirstClient, setup.ConversationId, poison.Id);
            Assert.Equal(ScheduledMessageStatus.Failed, terminal.Status);
            Assert.Equal("SCHEDULED_MAXIMUM_ATTEMPTS", terminal.LastFailureCode);
            await DispatchP2ScheduledAsync();
            Assert.Equal(3, (await factory.GetMessagesAsync(setup.ConversationId)).Length);
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }

    [Fact]
    public async Task Ph4ScheduledSqlRetryProgressesFiveTenTwentyFortyThenFailsAndRejectsEarlyPoll()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var now = DateTime.UtcNow; factory.Clock.SetUtcNow(now);
        try
        {
            var task = await ScheduleP2Async(setup.FirstClient, setup.ConversationId, new(now.AddMinutes(1),
                new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "شکست گذرای کنترل‌شده" }));
            var due = now.AddMinutes(2); factory.Clock.SetUtcNow(due);
            for (var attempt = 1; attempt <= ScheduledMessage.MaximumAttempts; attempt++)
            {
                await using (var scope = factory.Services.CreateAsyncScope())
                {
                    var store = scope.ServiceProvider.GetRequiredService<IScheduledMessageStore>();
                    var claim = await store.ClaimNextScheduledMessageAsync(default); Assert.NotNull(claim); Assert.Equal(task.Id, claim.Id);
                    await store.FailScheduledMessageAsync(claim, "CONTROLLED_TRANSIENT", true, default);
                }
                await using var check = factory.Services.CreateAsyncScope();
                var row = await check.ServiceProvider.GetRequiredService<MakanDbContext>().ScheduledMessages.AsNoTracking().SingleAsync(item => item.Id == task.Id);
                Assert.Equal(attempt, row.AttemptCount);
                if (attempt == ScheduledMessage.MaximumAttempts)
                {
                    Assert.Equal(ScheduledMessageStatus.Failed, row.Status); Assert.Null(row.NextAttemptAtUtc);
                }
                else
                {
                    var delay = 5 * (1 << (attempt - 1));
                    Assert.Equal(due.AddSeconds(delay), row.NextAttemptAtUtc);
                    factory.Clock.SetUtcNow(due.AddSeconds(delay - 1));
                    await using var early = factory.Services.CreateAsyncScope();
                    Assert.Null(await early.ServiceProvider.GetRequiredService<IScheduledMessageStore>().ClaimNextScheduledMessageAsync(default));
                    due = due.AddSeconds(delay); factory.Clock.SetUtcNow(due);
                }
            }
            await DispatchP2ScheduledAsync(); Assert.Empty(await factory.GetMessagesAsync(setup.ConversationId));
        }
        finally { await CleanupP2SchedulesAsync(setup.First.UserId); factory.Clock.UseSystemTime(); }
    }
}
