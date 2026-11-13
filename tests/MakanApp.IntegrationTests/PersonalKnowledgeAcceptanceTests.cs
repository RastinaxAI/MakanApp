using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    private static async Task PersonalKnowledgeContext(HttpClient client)
    {
        using var response = await client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Personal, Guid.NewGuid()), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task<EducationalSourceResult> PublishedPersonalKnowledge(HttpClient owner) =>
        await SourcePublish(owner, await SourceCreate(owner, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(owner, true)).Id)));

    private static async Task IngestPersonalKnowledge(HttpClient client, EducationalSourceResult source)
    {
        using var response = await client.PostAsJsonAsync(KnowledgePath(source, "ingest"),
            new KnowledgeMutationCommand(Guid.NewGuid(), source.Version, "پذیرش شخصی در SQL ایزوله"), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PersonalKnowledgeOrdinaryAccountUsesPersonalScopeAndIndividualPayerDespiteSchoolSwitch()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await PersonalKnowledgeContext(owner); await KnowledgeContract(SubscriptionOwner.Individual(user.UserId));
        var source = await PublishedPersonalKnowledge(owner);
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        await IngestPersonalKnowledge(client, source);
        var metadata = Assert.Single(control.IngestScopes);
        Assert.Equal("Personal", metadata.Type); Assert.Equal(user.UserId, metadata.OwnerUserId);
        Assert.Null(metadata.OrganizationId); Assert.Null(metadata.ClassId);
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش شخصی ساختگی", [new(source.SourceId, 1)]);
        using (var result = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve", query, JsonOptions)) result.EnsureSuccessStatusCode();
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Memberships.AnyAsync(m => m.UserId == user.UserId));

        var org = await factory.CreateOrganizationAsync("Workspace مدرسه مستقل از RAG");
        var membership = await factory.CreateMembershipAsync(user.UserId, org, OrganizationRole.Teacher);
        await SelectOrganizationAsync(owner, membership.MembershipId, OrganizationRole.Teacher);
        query = query with { ClientOperationId = Guid.NewGuid() };
        using (var result = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions)) result.EnsureSuccessStatusCode();
        await using var finalScope = factory.Services.CreateAsyncScope();
        var db = finalScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == query.ClientOperationId);
        Assert.Equal(CommerceContextType.Personal, reservation.ContextType); Assert.Equal(user.UserId, reservation.ContextId);
        Assert.Equal(SubscriptionOwnerType.Individual, reservation.OwnerType); Assert.Equal(user.UserId, reservation.OwnerId);
        Assert.False(reservation.IsTrial); Assert.Equal(UsageReservationStatus.Consumed, reservation.Status);
        Assert.All(control.Allowlists, list => Assert.Equal(new[] { new KnowledgeSourceVersion(source.SourceId, 1) }, list));
    }

    [Fact]
    public async Task PersonalKnowledgeExplicitTrialDoesNotDebitIndividualAndOrganizationPayerCannotInvoke()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await PersonalKnowledgeContext(owner); await KnowledgeContract(SubscriptionOwner.Individual(user.UserId), requests: 1);
        var source = await PublishedPersonalKnowledge(owner);
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        // Ingest مجوز و مانده را بررسی می‌کند؛ رزرو تولید متن فقط در RAG انجام می‌شود.
        await IngestPersonalKnowledge(client, source);
        var command = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش trial", [new(source.SourceId, 1)], Payer: AiPayerSelection.FreeTrial);
        using (var response = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", command, JsonOptions)) response.EnsureSuccessStatusCode();
        var calls = control.Calls;
        var denied = command with { ClientOperationId = Guid.NewGuid(), Payer = AiPayerSelection.Organization };
        using (var response = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", denied, JsonOptions)) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(calls, control.Calls);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == command.ClientOperationId);
        Assert.True(reservation.IsTrial); Assert.Null(reservation.SubscriptionId); Assert.Equal(user.UserId, reservation.OwnerId);
        Assert.False(await db.UsageReservations.AnyAsync(r => r.ConsumerUserId == user.UserId && !r.IsTrial));
        Assert.False(await db.UsageReservations.AnyAsync(r => r.OperationId == denied.ClientOperationId));
    }

    [Fact]
    public async Task PersonalKnowledgeConcurrentRagRetryHasOneGenerationAndOneReservation()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await PersonalKnowledgeContext(owner); await KnowledgeContract(SubscriptionOwner.Individual(user.UserId));
        var source = await PublishedPersonalKnowledge(owner);
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        await IngestPersonalKnowledge(client, source);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        control.BeforeRetrievalResponse = async () => { entered.TrySetResult(); await release.Task; };
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "قصد هم‌زمان ساختگی", [new(source.SourceId, 1)]);
        var first = client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            using var repeat = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions);
            Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
            Assert.Contains(AiInvocationErrorCodes.InProgress, await repeat.Content.ReadAsStringAsync());
        }
        finally { release.TrySetResult(); }
        using var completed = await first; completed.EnsureSuccessStatusCode(); Assert.Equal(1, control.RagCalls);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.CountAsync(r => r.OperationId == query.ClientOperationId));
    }

    [Fact]
    public async Task PersonalKnowledgeContextChangeDuringRetrievalReleasesWithoutPublishingEvidenceOrGenerating()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await PersonalKnowledgeContext(owner); await KnowledgeContract(SubscriptionOwner.Individual(user.UserId));
        var source = await PublishedPersonalKnowledge(owner);
        var org = await factory.CreateOrganizationAsync("تغییر صریح Assistant");
        var member = await factory.CreateMembershipAsync(user.UserId, org, OrganizationRole.Manager);
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        await IngestPersonalKnowledge(client, source);
        var currentAssistant = (await owner.GetFromJsonAsync<AssistantContextResult>("/api/v1/ai/assistant-context", JsonOptions))!;
        HttpStatusCode? selectionStatus = null;
        control.BeforeRetrievalResponse = async () =>
        {
            using var selected = await owner.PutAsJsonAsync("/api/v1/ai/assistant-context",
                new SelectAssistantContextCommand(AssistantDataContextKind.Organization, Guid.NewGuid(), member.MembershipId,
                    OrganizationRole.Manager, ExpectedVersion: currentAssistant.Version), JsonOptions);
            selectionStatus = selected.StatusCode;
        };
        var query = new KnowledgeQueryCommand(Guid.NewGuid(), "پرسش خصوصی ساختگی", [new(source.SourceId, 1)]);
        using var denied = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer", query, JsonOptions);
        Assert.Equal(HttpStatusCode.OK, selectionStatus);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.DoesNotContain("Synthetic knowledge evidence", await denied.Content.ReadAsStringAsync()); Assert.Equal(0, control.RagCalls);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(UsageReservationStatus.Released,
            (await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.SingleAsync(r => r.OperationId == query.ClientOperationId)).Status);
    }

    [Theory]
    [InlineData("archived")]
    [InlineData("suspended")]
    public async Task PersonalKnowledgeCurrentSourceAndAccountStateAreRecheckedBeforeOutbound(string mutation)
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        await PersonalKnowledgeContext(owner); await KnowledgeContract(SubscriptionOwner.Individual(user.UserId));
        var source = await PublishedPersonalKnowledge(owner);
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, owner);
        await IngestPersonalKnowledge(client, source); var calls = control.Calls;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            if (mutation == "archived") (await db.Set<EducationalSource>().SingleAsync(s => s.Id == source.SourceId)).Archive(factory.Clock.GetUtcNow().UtcDateTime);
            else (await db.Users.SingleAsync(u => u.Id == user.UserId)).Suspend(factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        }
        var operation = Guid.NewGuid();
        using var denied = await client.PostAsJsonAsync("/api/v1/ai/knowledge/rag/answer",
            new KnowledgeQueryCommand(operation, "پرسش", [new(source.SourceId, 1)]), JsonOptions);
        Assert.False(denied.IsSuccessStatusCode); Assert.Equal(calls, control.Calls); Assert.Equal(0, control.RagCalls);
        await using var finalScope = factory.Services.CreateAsyncScope();
        Assert.False(await finalScope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.AnyAsync(r => r.OperationId == operation));
    }

    [Fact]
    public async Task PersonalKnowledgeExplicitPersonalContextConcealsSchoolSourceDespiteValidTeacherMembership()
    {
        using var school = await CreateOfficialScenarioAsync(false); await KnowledgeContract(SubscriptionOwner.Organization(school.OrganizationId));
        var source = await SourcePublish(school.Teacher, await SourceCreate(school.Teacher,
            SourceCommand(EducationalSourceScope.Class, (await SourceUpload(school.Teacher, false)).Id, school.ClassId)));
        await PersonalKnowledgeContext(school.Teacher);
        var control = new KnowledgeTransport(); using var host = KnowledgeHost(control); using var client = KnowledgeClient(host, school.Teacher);
        using var denied = await client.PostAsJsonAsync("/api/v1/ai/knowledge/retrieve",
            new KnowledgeQueryCommand(Guid.NewGuid(), "نباید از Workspace مدرسه استفاده شود", [new(source.SourceId, 1)]), JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); Assert.Equal(0, control.Calls);
    }
}
