using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
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
    public async Task UnmeteredKnowledgeWithoutActiveSubscriptionPreservesPrivateSourceAclAndRagIdempotency()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup);
        await PersonalKnowledgeContext(setup);
        // فقط ایجاد و ذخیرهٔ فایل از سهمیهٔ مستقل Storage استفاده می‌کند؛ پیش از AI قرارداد لغو می‌شود.
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId), requests: 0, grantAi: false);
        var source = await PublishedPersonalKnowledge(setup);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var subscription = await db.Subscriptions.SingleAsync(s => s.OwnerId == user.UserId);
            subscription.Cancel(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync();
        }
        var control = new KnowledgeTransport(); using var baseHost = KnowledgeHost(control);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["AiUsage:Mode"] = "Unmetered" })));
        using var client = KnowledgeClient(host, setup); await IngestPersonalKnowledge(client, source);
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش دانش نامحدود", [new(source.SourceId, source.Revision)]);
        using (var retrieved = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, JsonOptions)) retrieved.EnsureSuccessStatusCode();
        for (var index = 0; index < 4; index++)
        {
            query = query with { ClientOperationId = Guid.NewGuid(), Payer = index % 2 == 0 ? AiPayerSelection.Organization : AiPayerSelection.FreeTrial };
            using var result = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions); result.EnsureSuccessStatusCode();
        }
        using (var repeated = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query with { Payer = null }, JsonOptions))
        {
            Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
            Assert.Contains(AiInvocationErrorCodes.Completed, await repeated.Content.ReadAsStringAsync());
        }
        Assert.Equal(4, control.RagCalls);
        using var outsiderSetup = factory.CreateClient(); await CreateMessagingUserAsync(outsiderSetup); await PersonalKnowledgeContext(outsiderSetup);
        using var outsider = KnowledgeClient(host, outsiderSetup); var calls = control.Calls;
        using (var forbidden = await outsider.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query with { ClientOperationId = Guid.NewGuid() }, JsonOptions))
            Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        Assert.Equal(calls, control.Calls);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            var records = await db.UsageReservations.Where(r => r.ConsumerUserId == user.UserId).ToArrayAsync();
            Assert.Equal(4, records.Length);
            Assert.All(records, r => { Assert.True(r.IsUnmetered); Assert.False(r.IsTrial); Assert.Null(r.SubscriptionId); Assert.Equal(UsageReservationStatus.Consumed, r.Status); });
            Assert.Equal(4, await db.UsageLedgerEntries.CountAsync(e => e.ConsumerUserId == user.UserId && e.IsUnmetered && e.Status == UsageReservationStatus.Consumed));
            Assert.Equal(1, await db.Subscriptions.CountAsync(s => s.OwnerId == user.UserId));
            Assert.False(await db.Subscriptions.AnyAsync(s => s.OwnerId == user.UserId && s.Status == SubscriptionStatus.Active));
            Assert.False(await db.Orders.AnyAsync(o => o.PurchaserUserId == user.UserId));
            (await db.Set<EducationalSource>().SingleAsync(s => s.Id == source.SourceId)).Archive(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync();
        }
        using (var archived = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query with { ClientOperationId = Guid.NewGuid() }, JsonOptions))
        {
            Assert.Equal(HttpStatusCode.Conflict, archived.StatusCode);
            Assert.Contains(SourceErrorCodes.NotReady, await archived.Content.ReadAsStringAsync());
        }
        Assert.Equal(calls, control.Calls); Assert.Equal(4, control.RagCalls);
    }

    [Fact]
    public async Task UnmeteredKnowledgeRechecksCurrentSourceAfterRetrievalAndConcealsEvidence()
    {
        using var setup = factory.CreateClient(); var user = await CreateMessagingUserAsync(setup); await PersonalKnowledgeContext(setup);
        await KnowledgeContract(SubscriptionOwner.Individual(user.UserId), requests: 0, grantAi: false); var source = await PublishedPersonalKnowledge(setup);
        var control = new KnowledgeTransport(); using var baseHost = KnowledgeHost(control);
        using var host = baseHost.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["AiUsage:Mode"] = "Unmetered" })));
        using var client = KnowledgeClient(host, setup); await IngestPersonalKnowledge(client, source);
        control.BeforeRetrievalResponse = async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Set<EducationalSource>().SingleAsync(s => s.Id == source.SourceId)).Archive(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync();
        };
        var operation = Guid.NewGuid();
        using var result = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer",
            new KnowledgeQueryCommand(operation, "پرسش با لغو جاری مجوز", [new(source.SourceId, source.Revision)]), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode); Assert.Equal(0, control.RagCalls);
        Assert.Contains(SourceErrorCodes.NotReady, await result.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Synthetic knowledge evidence", await result.Content.ReadAsStringAsync());
        await using var finalScope = factory.Services.CreateAsyncScope();
        var record = await finalScope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.SingleAsync(r => r.OperationId == operation);
        Assert.True(record.IsUnmetered); Assert.Equal(UsageReservationStatus.Released, record.Status);
    }
}
