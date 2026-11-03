using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(OrganizationRole.Student)]
    [InlineData(OrganizationRole.Parent)]
    [InlineData(OrganizationRole.Teacher)]
    [InlineData(OrganizationRole.Manager)]
    public async Task PersonalClassV2AnyActiveAccountCanRequestWithoutNewSchoolAuthority(OrganizationRole? role)
    {
        var actor = await ActorAsync();
        if (role.HasValue)
        {
            var org = await factory.CreateOrganizationAsync("نقش مستقل از درخواست");
            await factory.CreateMembershipAsync(actor.UserId, org, role.Value);
        }
        var before = await WithDb(db => db.Memberships.CountAsync(m => m.UserId == actor.UserId));
        var request = await SubmittedClassRequestAsync(actor, "درخواست بدون وابستگی نقش");
        Assert.Equal(PersonalClassRequestStatus.Submitted, request.Status); Assert.Null(request.CreatedClassId);
        await WithDb(async db =>
        {
            Assert.Equal(before, await db.Memberships.CountAsync(m => m.UserId == actor.UserId));
            Assert.False(await db.PersonalClasses.AnyAsync(c => c.FormationRequestId == request.Id));
            Assert.False(await db.Conversations.AnyAsync(c => c.PersonalClassId != null && db.PersonalClasses.Any(k => k.Id == c.PersonalClassId && k.FormationRequestId == request.Id)));
        });
    }

    [Fact]
    public async Task PersonalClassV2RequestIdempotencyVersionChangesAndReviewHistoryAreExplicit()
    {
        var actor = await ActorAsync(); var admin = await AdminAsync();
        var command = new CreatePersonalClassRequestCommand(Guid.NewGuid(), new("طرح اولیه", "فعالیت", null, 12, null, null));
        var draft = await PostAsync<PersonalClassRequestDto>(actor.Client, "/api/v1/personal-class-requests", command);
        Assert.True(draft.Capabilities.CanEdit);
        var replay = await PostAsync<PersonalClassRequestDto>(actor.Client, "/api/v1/personal-class-requests", command);
        Assert.Equal(draft.Id, replay.Id);
        using var mismatch = await actor.Client.PostAsJsonAsync("/api/v1/personal-class-requests", command with { Details = command.Details with { SuggestedCapacity = 13 } }, Json);
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        var submitted = await PostAsync<PersonalClassRequestDto>(actor.Client, $"/api/v1/personal-class-requests/{draft.Id}/submit", new PersonalClassVersionCommand(draft.Version));
        var needs = await PostAsync<PersonalClassRequestDto>(admin.Client, $"/api/admin/v1/personal-class-requests/{draft.Id}/review",
            new ReviewPersonalClassRequestCommand(submitted.Version, PersonalClassRequestStatus.NeedsChanges, "هدف تکمیل شود"));
        Assert.True(needs.Capabilities.CanEdit == (needs.RequesterUserId == admin.UserId));
        var own = await GetAsync<PersonalClassRequestDto>(actor.Client, $"/api/v1/personal-class-requests/{draft.Id}");
        Assert.True(own.Capabilities.CanRespondToChanges);
        using var stale = await actor.Client.PutAsJsonAsync($"/api/v1/personal-class-requests/{draft.Id}", new EditPersonalClassRequestCommand(draft.Version, command.Details), Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var editedResponse = await actor.Client.PutAsJsonAsync($"/api/v1/personal-class-requests/{draft.Id}", new EditPersonalClassRequestCommand(own.Version, command.Details with { Purpose = "هدف تکمیل‌شده" }), Json);
        editedResponse.EnsureSuccessStatusCode(); var edited = (await editedResponse.Content.ReadFromJsonAsync<PersonalClassRequestDto>(Json))!;
        var resubmitted = await PostAsync<PersonalClassRequestDto>(actor.Client, $"/api/v1/personal-class-requests/{draft.Id}/submit", new PersonalClassVersionCommand(edited.Version));
        var rejected = await PostAsync<PersonalClassRequestDto>(admin.Client, $"/api/admin/v1/personal-class-requests/{draft.Id}/review",
            new ReviewPersonalClassRequestCommand(resubmitted.Version, PersonalClassRequestStatus.Rejected, "عدم تأیید نهایی"));
        Assert.Null(rejected.CreatedClassId);
        var history = await GetAsync<PersonalClassPage<PersonalClassDecisionDto>>(actor.Client, $"/api/v1/personal-class-requests/{draft.Id}/history");
        Assert.Contains(history.Items, h => h.Action.EndsWith("NeedsChanges")); Assert.Contains(history.Items, h => h.Action.EndsWith("Rejected"));
        Assert.All(history.Items, h => Assert.Null(h.ActorUserId));
        Assert.Equal(draft.Id, (await PostAsync<PersonalClassRequestDto>(actor.Client, "/api/v1/personal-class-requests", command)).Id);
    }

    [Fact]
    public async Task PersonalClassV2DirectCreateAndSchoolManagerReviewCannotBypassPlatform()
    {
        var actor = await ActorAsync(); var manager = await ActorAsync();
        var org = await factory.CreateOrganizationAsync("مرز بررسی پلتفرم"); await factory.CreateMembershipAsync(manager.UserId, org, OrganizationRole.Manager);
        var request = await SubmittedClassRequestAsync(actor, "مرز تشکیل");
        using var direct = await actor.Client.PostAsJsonAsync("/api/v1/personal-classes", new CreatePersonalClassCommand("میان‌بر", Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.Conflict, direct.StatusCode); Assert.Equal(AcademicErrorCodes.PersonalClassReviewRequired, await ProblemAsync(direct));
        using var denied = await manager.Client.PostAsJsonAsync($"/api/admin/v1/personal-class-requests/{request.Id}/review",
            new ReviewPersonalClassRequestCommand(request.Version, PersonalClassRequestStatus.Approved, "بدون مجوز"), Json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task PersonalClassV2ConcurrentApprovalAndProvisioningCreateOneClassAndOnePair()
    {
        var host = await ActorAsync(); var admin = await AdminAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var request = await SubmittedClassRequestAsync(host, "بررسی همزمان");
        var command = new ReviewPersonalClassRequestCommand(request.Version, PersonalClassRequestStatus.Approved, "تأیید همزمان");
        var approvals = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => PostAsync<PersonalClassRequestDto>(admin.Client, $"/api/admin/v1/personal-class-requests/{request.Id}/review", command)));
        var id = approvals[0].CreatedClassId!.Value; Assert.Equal(id, approvals[1].CreatedClassId);
        var pending = await GetAsync<MakanApp.Application.Messaging.OfficialSpacesPairResult>(host.Client, $"/api/v1/personal-classes/{id}/spaces");
        Assert.Null(pending.Group.ConversationId); Assert.Null(pending.Channel.ConversationId);
        await WithDb(async db => Assert.Equal(0, await db.Conversations.CountAsync(c => c.PersonalClassId == id)));
        async Task<bool> Provision()
        { await using var scope = factory.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<EfOfficialSpacesStore>().EnsurePersonalClassPairAsync(id, default); }
        Assert.All(await Task.WhenAll(Provision(), Provision()), Assert.True);
        await WithDb(async db =>
        {
            Assert.Equal(1, await db.PersonalClasses.CountAsync(c => c.FormationRequestId == request.Id));
            Assert.Equal(2, await db.Conversations.CountAsync(c => c.PersonalClassId == id));
            Assert.False(await db.Conversations.AnyAsync(c => c.PersonalClassId == id && (c.OrganizationId != null || c.CreatedByUserId != null)));
            Assert.False(await db.AcademicActivityPublications.AnyAsync(a => a.ClassId == id));
            Assert.Equal(PersonalClassLocalRoles.Manager, (await db.PersonalClassParticipants.SingleAsync(m => m.PersonalClassId == id)).LocalRoles);
        });
    }

    [Fact]
    public async Task PersonalClassV2InvitationAndJoinRaceConvergeWithoutDuplicateMembership()
    {
        var host = await ActorAsync(); var learner = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "دو قصد یک عضویت", 1);
        using var closed = await learner.Client.PostAsJsonAsync($"/api/v1/personal-classes/{c.Id}/join-requests", new CreatePersonalClassJoinCommand(Guid.NewGuid()), Json);
        Assert.Equal(HttpStatusCode.NotFound, closed.StatusCode);
        using var settings = await host.Client.PutAsJsonAsync($"/api/v1/personal-classes/{c.Id}/settings", new ConfigurePersonalClassCommand(c.Version, 1, true, "پذیرش صریح درخواست"), Json);
        settings.EnsureSuccessStatusCode();
        var join = await PostAsync<PersonalClassJoinDto>(learner.Client, $"/api/v1/personal-classes/{c.Id}/join-requests", new CreatePersonalClassJoinCommand(Guid.NewGuid()));
        var invite = await InviteAsync(host, learner, c.Id);
        var results = await Task.WhenAll(learner.Client.PostAsync($"/api/v1/personal-classes/invitations/{invite.Id}/accept", null),
            host.Client.PostAsJsonAsync($"/api/v1/personal-classes/join-requests/{join.Id}/decision", new DecidePersonalClassJoinCommand(join.Version, PersonalClassJoinRequestStatus.Approved, "پذیرش مدیر"), Json));
        try { foreach (var r in results) r.EnsureSuccessStatusCode(); } finally { foreach (var r in results) r.Dispose(); }
        await WithDb(async db => Assert.Equal(1, await db.PersonalClassParticipants.CountAsync(m => m.PersonalClassId == c.Id && m.StudentUserId == learner.UserId && m.EndedAtUtc == null)));
    }

    [Fact]
    public async Task PersonalClassV2LastEducationalSeatIsAtomicAndManagersDoNotConsumeIt()
    {
        var host = await ActorAsync(); var first = await ActorAsync(); var second = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true, learners: 20));
        var c = await ApprovedClassAsync(host, "آخرین ظرفیت آموزشی", 1);
        var a = await InviteAsync(host, first, c.Id); var b = await InviteAsync(host, second, c.Id);
        var results = await Task.WhenAll(first.Client.PostAsync($"/api/v1/personal-classes/invitations/{a.Id}/accept", null), second.Client.PostAsync($"/api/v1/personal-classes/invitations/{b.Id}/accept", null));
        try
        {
            Assert.Single(results, r => r.IsSuccessStatusCode); var failed = Assert.Single(results, r => !r.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, failed.StatusCode); Assert.Equal(AcademicErrorCodes.PersonalClassCapacityReached, await ProblemAsync(failed));
        }
        finally { foreach (var r in results) r.Dispose(); }
    }

    [Fact]
    public async Task PersonalClassV2LastManagerTransferNeedsConsentAndDoesNotChangeCommercialHost()
    {
        var host = await ActorAsync(); var next = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "انتقال مسئولیت");
        var member = Assert.Single((await GetAsync<PersonalClassPage<PersonalClassMembershipDto>>(host.Client, $"/api/v1/personal-classes/{c.Id}/memberships")).Items);
        using var leave = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/personal-classes/{c.Id}/memberships/{member.Id}")
        { Content = JsonContent.Create(new PersonalClassReasonCommand(member.Version, "خروج بدون تحویل"), options: Json) };
        using var denied = await host.Client.SendAsync(leave); Assert.Equal(AcademicErrorCodes.PersonalClassLastManagerRequired, await ProblemAsync(denied));
        var transfer = await PostAsync<PersonalClassTransferDto>(host.Client, $"/api/v1/personal-classes/{c.Id}/manager-transfers", new CreatePersonalClassTransferCommand(c.Version, next.UserId, Guid.NewGuid(), "تحویل معتبر"));
        Assert.Equal(host.UserId, (await GetAsync<PersonalClassDto>(host.Client, $"/api/v1/personal-classes/{c.Id}")).ManagerUserId);
        await PostAsync<PersonalClassTransferDto>(next.Client, $"/api/v1/personal-classes/manager-transfers/{transfer.Id}/decision", new DecidePersonalClassTransferCommand(transfer.Version, PersonalClassManagerTransferStatus.Accepted));
        var updated = await GetAsync<PersonalClassDto>(next.Client, $"/api/v1/personal-classes/{c.Id}"); Assert.Equal(host.UserId, updated.HostUserId); Assert.Equal(next.UserId, updated.ManagerUserId);
        using var old = await host.Client.GetAsync($"/api/v1/personal-classes/{c.Id}"); Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }
}
