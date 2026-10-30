using MakanApp.Application.Commerce;
using MakanApp.Application.Academic;
using MakanApp.Application.Realtime;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task AccountRealtimePersonalClassCapacityChangesNotifyHostWithoutExposingHostFinanceToStudent()
    {
        var host = await ActorAsync(); var student = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var hostCursor = (await GetAsync<AccountChangePageResult>(host.Client, "/api/v1/realtime/changes")).NextCursor;
        var studentCursor = (await GetAsync<AccountChangePageResult>(student.Client, "/api/v1/realtime/changes")).NextCursor;
        var classroom = await PostAsync<PersonalClassDto>(host.Client, "/api/v1/personal-classes", new CreatePersonalClassCommand("کلاس ایزولهٔ ظرفیت", Guid.NewGuid()));
        var invitation = await PostAsync<PersonalClassInvitationDto>(host.Client, $"/api/v1/personal-classes/{classroom.Id}/invitations", new InvitePersonalClassStudentCommand(student.UserId, Guid.NewGuid()));
        using var accepted = await student.Client.PostAsync($"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
        accepted.EnsureSuccessStatusCode();
        var hostChanges = await GetAsync<AccountChangePageResult>(host.Client, "/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(hostCursor));
        Assert.Contains(hostChanges.Changes, x => x.Invalidation?.Key == "commerce");
        var childChanges = await GetAsync<AccountChangePageResult>(student.Client, "/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(studentCursor));
        Assert.Contains(childChanges.Changes, x => x.Invalidation?.Key == "access");
        Assert.DoesNotContain(childChanges.Changes, x => x.Invalidation?.Key == "commerce");
        var quota = await GetAsync<CommercialSummaryDto>(host.Client, "/api/v1/commerce/personal/subscription");
        Assert.Equal(1, quota.Quotas.Single(q => q.Key == QuotaKey.ActiveLearners).Consumed);
        Assert.Equal(SubscriptionDisplayState.NoSubscription, (await GetAsync<CommercialSummaryDto>(student.Client, "/api/v1/commerce/personal/subscription")).State);
    }

    [Fact]
    public async Task AccountRealtimePersonalQuotaReservationAndFinalizationNeverLeakToSchoolManagerOrDoubleConsume()
    {
        var owner = await ActorAsync(); var manager = await ActorAsync(); var outsider = await ActorAsync();
        var organization = await factory.CreateOrganizationAsync("سازمان ایزولهٔ آزمون حریم مالی");
        var membership = await factory.CreateMembershipAsync(manager.UserId, organization, OrganizationRole.Manager);
        await SelectAsync(manager, membership.MembershipId, OrganizationRole.Manager);
        await factory.CreateMembershipAsync(owner.UserId, organization, OrganizationRole.Student);
        await SubscribeAsync(SubscriptionOwner.Individual(owner.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 5));
        var ownerCursor = (await GetAsync<AccountChangePageResult>(owner.Client, "/api/v1/realtime/changes")).NextCursor;
        var managerCursor = (await GetAsync<AccountChangePageResult>(manager.Client, "/api/v1/realtime/changes")).NextCursor;
        var otherCursor = (await GetAsync<AccountChangePageResult>(outsider.Client, "/api/v1/realtime/changes")).NextCursor;
        var context = new AiQuotaPreflightCommand(CommerceContextType.Personal, Payer: AiPayerSelection.Individual);
        var operation = Guid.NewGuid();
        var reservation = await ReserveAsync(owner, context, operation);
        var replay = await ReserveAsync(owner, context, operation);
        Assert.True(replay.Replayed);
        await FinishAsync(owner, reservation, UsageReservationStatus.Consumed);
        await FinishAsync(owner, reservation, UsageReservationStatus.Consumed);
        var visible = await GetAsync<AccountChangePageResult>(owner.Client, "/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(ownerCursor));
        Assert.Equal(2, visible.Changes.Count(x => x.Invalidation?.Key == "commerce"));
        Assert.All(visible.Changes.Where(x => x.Invalidation is not null), x => Assert.Null(x.Invalidation!.ResourceId));
        var staff = await GetAsync<AccountChangePageResult>(manager.Client, "/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(managerCursor));
        var other = await GetAsync<AccountChangePageResult>(outsider.Client, "/api/v1/realtime/changes?cursor=" + Uri.EscapeDataString(otherCursor));
        Assert.DoesNotContain(staff.Changes, x => x.Invalidation?.Key == "commerce");
        Assert.DoesNotContain(other.Changes, x => x.Invalidation?.Key == "commerce");
        var after = await PostAsync<AiQuotaPreflightDto>(owner.Client, "/api/v1/commerce/ai/simple-text/preflight", context);
        Assert.Equal(4, after.Remaining);
    }
}
