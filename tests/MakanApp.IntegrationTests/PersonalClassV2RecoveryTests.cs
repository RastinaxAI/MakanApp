using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Messaging;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalClassV2ChannelFailurePreservesGroupAndRetryRepairsOnlyMissingDestination()
    {
        var host = await ActorAsync(); var admin = await AdminAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var request = await SubmittedClassRequestAsync(host, "بازیابی مقصد شکست‌خورده");
        var approved = await PostAsync<PersonalClassRequestDto>(admin.Client, $"/api/admin/v1/personal-class-requests/{request.Id}/review",
            new ReviewPersonalClassRequestCommand(request.Version, PersonalClassRequestStatus.Approved, "آزمون بازیابی"));
        var id = approved.CreatedClassId!.Value;
        await using var scope = factory.Services.CreateAsyncScope();
        var options = new DbContextOptionsBuilder<MakanDbContext>(scope.ServiceProvider.GetRequiredService<DbContextOptions<MakanDbContext>>())
            .AddInterceptors(new FailPersonalClassChannelOnce(id)).Options;
        await using var db = new MakanDbContext(options, factory.Clock);
        var store = new EfOfficialSpacesStore(db, scope.ServiceProvider.GetRequiredService<IOutbox>(), factory.Clock, NullLogger<EfOfficialSpacesStore>.Instance);
        Assert.False(await store.EnsurePersonalClassPairAsync(id, default));
        var group = await db.Conversations.AsNoTracking().SingleAsync(c => c.PersonalClassId == id);
        Assert.Equal(ConversationType.Group, group.Type);
        var first = await db.OfficialSpaceProvisions.AsNoTracking().Where(p => p.PersonalClassId == id).ToArrayAsync();
        Assert.Equal(OfficialSpaceState.Ready, first.Single(p => p.Purpose == ConversationType.Group).State);
        var failed = first.Single(p => p.Purpose == ConversationType.Channel);
        Assert.Equal(OfficialSpaceState.RepairRequired, failed.State); Assert.Equal("PersistenceFailure", failed.LastFailureCategory);
        Assert.Equal(PersonalClassState.ProvisioningFailed, (await db.PersonalClasses.AsNoTracking().SingleAsync(c => c.Id == id)).State);
        Assert.True(await store.EnsurePersonalClassPairAsync(id, default));
        Assert.Equal(group.Id, (await db.Conversations.AsNoTracking().SingleAsync(c => c.PersonalClassId == id && c.Type == ConversationType.Group)).Id);
        var final = await db.OfficialSpaceProvisions.AsNoTracking().Where(p => p.PersonalClassId == id).ToArrayAsync();
        Assert.All(final, p => Assert.Equal(OfficialSpaceState.Ready, p.State));
        Assert.Equal(first.Single(p => p.Purpose == ConversationType.Group).AttemptCount, final.Single(p => p.Purpose == ConversationType.Group).AttemptCount);
        Assert.Equal(failed.AttemptCount + 1, final.Single(p => p.Purpose == ConversationType.Channel).AttemptCount);
        Assert.Equal(2, await db.Conversations.CountAsync(c => c.PersonalClassId == id));
    }

    [Fact]
    public async Task PersonalClassV2LegacyRequiresCapacityAndApprovedDryRunPreservesGroupWithoutHistoricalSpam()
    {
        var host = await ActorAsync(); var learner = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var id = await WithDb(async db =>
        {
            var c = PersonalClass.Create(host.UserId, "کلاس قدیمی مصنوعی", Guid.NewGuid(), factory.Clock.GetUtcNow().UtcDateTime);
            c.SetManager(host.UserId); db.PersonalClasses.Add(c);
            db.PersonalClassParticipants.Add(PersonalClassParticipant.Admit(c.Id, host.UserId, PersonalClassLocalRoles.Manager, c.CreatedAtUtc));
            db.Conversations.Add(Conversation.CreatePersonalClassSpace(ConversationType.Group, c.Id, "گروه موجود", c.CreatedAtUtc));
            await db.SaveChangesAsync(); return c.Id;
        });
        var c = await GetAsync<PersonalClassDto>(host.Client, $"/api/v1/personal-classes/{id}"); Assert.Null(c.Capacity);
        using var denied = await host.Client.PostAsJsonAsync($"/api/v1/personal-classes/{id}/invitations", new InvitePersonalClassStudentCommand(learner.UserId, Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        using var configured = await host.Client.PutAsJsonAsync($"/api/v1/personal-classes/{id}/settings", new ConfigurePersonalClassCommand(c.Version, 2, false, "ظرفیت صریح"), Json);
        configured.EnsureSuccessStatusCode(); c = (await configured.Content.ReadFromJsonAsync<PersonalClassDto>(Json))!;
        await using var scope = factory.Services.CreateAsyncScope(); var store = scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>();
        Assert.False(await store.EnsurePersonalClassPairAsync(id, default));
        var plan = await GetAsync<PersonalClassBackfillPlanRow>(host.Client, $"/api/v1/personal-classes/{id}/spaces/backfill-plan");
        Assert.False(plan.Conflict); Assert.Equal("Reuse", plan.GroupAction); Assert.Equal("CreateMissing", plan.ChannelAction);
        using var missingApproval = await host.Client.PostAsJsonAsync($"/api/v1/personal-classes/{id}/spaces/repair", new RepairPersonalClassSpacesCommand(c.Version, "بدون طرح"), Json);
        Assert.Equal(HttpStatusCode.Conflict, missingApproval.StatusCode);
        using var repair = await host.Client.PostAsJsonAsync($"/api/v1/personal-classes/{id}/spaces/repair", new RepairPersonalClassSpacesCommand(c.Version, "تأیید طرح", plan.PlanSha256), Json);
        repair.EnsureSuccessStatusCode(); Assert.True(await store.EnsurePersonalClassPairAsync(id, default)); Assert.True(await store.EnsurePersonalClassPairAsync(id, default));
        await WithDb(async db =>
        {
            Assert.Equal(plan.GroupCandidateId, (await db.Conversations.SingleAsync(s => s.PersonalClassId == id && s.Type == ConversationType.Group)).Id);
            Assert.Equal(2, await db.Conversations.CountAsync(s => s.PersonalClassId == id));
            Assert.False(await db.Messages.AnyAsync(m => db.Conversations.Any(s => s.Id == m.ConversationId && s.PersonalClassId == id)));
        });
    }

    [Fact]
    public async Task PersonalClassV2SameJoinOperationAcrossDifferentClassesReturnsConflictWithoutDuplicate()
    {
        var host = await ActorAsync(); var learner = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var a = await ApprovedClassAsync(host, "مقصد یک"); var b = await ApprovedClassAsync(host, "مقصد دو");
        foreach (var c in new[] { a, b })
        { using var r = await host.Client.PutAsJsonAsync($"/api/v1/personal-classes/{c.Id}/settings", new ConfigurePersonalClassCommand(c.Version, 10, true, "پذیرش درخواست"), Json); r.EnsureSuccessStatusCode(); }
        var command = new CreatePersonalClassJoinCommand(Guid.NewGuid());
        var responses = await Task.WhenAll(new[] { a, b }.Select(c => learner.Client.PostAsJsonAsync($"/api/v1/personal-classes/{c.Id}/join-requests", command, Json)));
        try { Assert.Single(responses, r => r.IsSuccessStatusCode); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var r in responses) r.Dispose(); }
        await WithDb(async db => Assert.Equal(1, await db.PersonalClassJoinRequests.CountAsync(j => j.UserId == learner.UserId && j.ClientOperationId == command.ClientOperationId)));
    }

    private sealed class FailPersonalClassChannelOnce(Guid classId) : SaveChangesInterceptor
    {
        private bool _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<Conversation>().Any(e => e.State == EntityState.Added && e.Entity.PersonalClassId == classId && e.Entity.Type == ConversationType.Channel))
            { _failed = true; throw new DbUpdateException("شکست ساختگی و یک‌بارهٔ مقصد دوم"); }
            return ValueTask.FromResult(result);
        }
    }
}
