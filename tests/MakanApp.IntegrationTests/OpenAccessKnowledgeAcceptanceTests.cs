using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task OpenAccessCreatesAndUsesPersonalKnowledgeWithoutAnyPlanButPreservesSourceAclAndReplay()
    {
        using var setup = factory.CreateClient();
        var user = await CreateMessagingUserAsync(setup);
        await PersonalKnowledgeContext(setup);
        var control = new KnowledgeTransport();
        using var baseHost = KnowledgeHost(control);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Commerce:EnforcementEnabled"] = "false",
                ["AiUsage:Mode"] = "Commercial"
            })));
        using var client = KnowledgeClient(host, setup);
        Assert.False(host.Services.GetRequiredService<CommercialOptions>().EnforcementEnabled);
        Assert.True(host.Services.GetRequiredService<AiUsageOptions>().IsUnmetered);
        var source = await PublishedPersonalKnowledge(client);
        await IngestPersonalKnowledge(client, source);
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش ایزوله از منبع شخصی", [new(source.SourceId, source.Revision)]);
        using (var answer = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions))
            answer.EnsureSuccessStatusCode();
        using (var replay = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions))
        {
            Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
            Assert.Contains(AiInvocationErrorCodes.Completed, await replay.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, control.RagCalls);
        using var outsiderSetup = factory.CreateClient();
        await CreateMessagingUserAsync(outsiderSetup);
        await PersonalKnowledgeContext(outsiderSetup);
        using var outsider = KnowledgeClient(host, outsiderSetup);
        var before = control.Calls;
        using (var denied = await outsider.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query with { ClientOperationId = Guid.NewGuid() }, JsonOptions))
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(before, control.Calls);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await db.Subscriptions.AnyAsync(s => s.OwnerId == user.UserId));
        Assert.False(await db.Orders.AnyAsync(o => o.PurchaserUserId == user.UserId));
        var reservation = await db.UsageReservations.SingleAsync(r => r.ConsumerUserId == user.UserId);
        Assert.True(reservation.IsUnmetered);
        Assert.Null(reservation.SubscriptionId);
        Assert.Null(reservation.PlanVersionId);
        Assert.Equal(UsageReservationStatus.Consumed, reservation.Status);
    }
}
