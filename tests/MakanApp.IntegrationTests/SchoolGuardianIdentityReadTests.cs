using System.Net.Http.Json;
using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task SchoolGuardianIdentityComesFromScopedRelationEvenWithoutParentMembership()
    {
        var org = await factory.CreateOrganizationAsync("خواندن هویت رابطه " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        var learner = await SchoolActorAsync(org, OrganizationRole.Student); using var lc = learner.Client;
        using var parentClient = _host.CreateClient(); var parent = await CreateP0PersonalAccountAsync(parentClient, null);
        Guid id;
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            Assert.False(await db.Memberships.AnyAsync(x => x.UserId == parent.UserId && x.OrganizationId == org));
            var relation = GuardianRelation.CreatePending(org, parent.UserId, learner.OrganizationPersonId, factory.Clock.GetUtcNow().UtcDateTime);
            relation.Activate(factory.Clock.GetUtcNow().UtcDateTime); db.GuardianRelations.Add(relation); await db.SaveChangesAsync(); id = relation.Id;
        }
        var dto = (await m.GetFromJsonAsync<SchoolGuardianRelationDto>($"/api/v1/organization/guardian-relations/{id}", PolicyJson))!;
        Assert.Equal(parent.UserId, dto.GuardianUserId); Assert.Equal(parent.Username, dto.GuardianUsername);
        Assert.False(string.IsNullOrWhiteSpace(dto.GuardianDisplayName)); Assert.False(string.IsNullOrWhiteSpace(dto.LearnerDisplayName));
        Assert.DoesNotContain("phone", await m.GetStringAsync($"/api/v1/organization/guardian-relations/{id}"), StringComparison.OrdinalIgnoreCase);
    }
}
