using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Domain.Commerce;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalInvitationShowsScopedClassAndHistoricalInviterWithoutGlobalIdentityLookup()
    {
        var host = await ActorAsync(); var learner = await ActorAsync(); var outsider = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "عنوان کلاس در دعوت");
        var invitation = await InviteAsync(host, learner, c.Id);
        var rows = (await learner.Client.GetFromJsonAsync<PersonalClassInvitationDto[]>("/api/v1/personal-classes/invitations", Json))!;
        var row = Assert.Single(rows, i => i.Id == invitation.Id);
        Assert.Equal(c.Title, row.ClassTitle); Assert.NotNull(row.Inviter); Assert.Equal(host.UserId, row.Inviter.UserId);
        Assert.NotEmpty(row.Inviter.DisplayName);
        Assert.DoesNotContain(host.Phone, await learner.Client.GetStringAsync("/api/v1/personal-classes/invitations"));
        Assert.DoesNotContain((await outsider.Client.GetFromJsonAsync<PersonalClassInvitationDto[]>("/api/v1/personal-classes/invitations", Json))!, i => i.Id == invitation.Id);
    }
}
