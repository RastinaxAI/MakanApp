using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Realtime;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AccountReplayConcurrentCommittedBatchesHaveNoMissingEventAcrossSmallPages()
    {
        using var client = factory.CreateClient(); var owner = await CreateMessagingUserAsync(client);
        var cursor = (await AccountChangesAsync(client)).NextCursor;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var firstCommitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstPage = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new ConcurrentBag<Guid>();
        var seen = new HashSet<Guid>();
        var producer = ProduceAsync();
        await firstCommitted.Task.WaitAsync(budget.Token);
        try
        {
            for (var page = 0; page < 200; page++)
            {
                using var response = await client.GetAsync("/api/v1/realtime/changes?limit=3&cursor=" + Uri.EscapeDataString(cursor), budget.Token);
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    Assert.Equal(RealtimeErrorCodes.Unavailable, await ReadProblemCodeAsync(response));
                    await Task.Delay(10, budget.Token);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                var next = (await response.Content.ReadFromJsonAsync<AccountChangePageResult>(JsonOptions, budget.Token))!;
                foreach (var item in next.Changes) seen.Add(item.EventId);
                cursor = next.NextCursor;
                firstPage.TrySetResult();
                if (producer.IsCompleted && !next.HasMore) break;
                if (next.RetryAfterMilliseconds is int delay) await Task.Delay(delay, budget.Token);
            }
            await producer;
            Assert.Equal(30, expected.Count);
            Assert.All(expected, id => Assert.Contains(id, seen));
        }
        finally { firstPage.TrySetResult(); await producer; }

        async Task ProduceAsync()
        {
            for (var batch = 0; batch < 10; batch++)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                for (var item = 0; item < 3; item++) expected.Add(AddOwnAccessEvent(scope.ServiceProvider, owner.UserId));
                await scope.ServiceProvider.GetRequiredService<MakanDbContext>().SaveChangesAsync(budget.Token);
                if (batch == 0) { firstCommitted.TrySetResult(); await firstPage.Task.WaitAsync(budget.Token); }
            }
        }
    }
}
