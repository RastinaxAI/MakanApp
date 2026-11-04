using System.Net;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class PlatformAuthorizationRegressionTests
{
    [Fact]
    public async Task SchoolSentInvitationsReloadsEffectiveStatusesAndFiltersWithinCurrentOrganization()
    {
        var org = await factory.CreateOrganizationAsync("دعوت‌های ارسال‌شده " + Guid.NewGuid());
        var manager = await SchoolActorAsync(org, OrganizationRole.Manager); using var m = manager.Client;
        var target = await SchoolActorAsync(org, OrganizationRole.Student); using var t = target.Client;
        var foreign = await factory.CreateOrganizationAsync("سازمان دیگر دعوت " + Guid.NewGuid());
        var otherManager = await SchoolActorAsync(foreign, OrganizationRole.Manager); using var other = otherManager.Client;
        Guid pendingId, expiredId;
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var now = factory.Clock.GetUtcNow().UtcDateTime;
            var pending = Invitation.CreatePersonLink(org, target.OrganizationPersonId, target.UserId, manager.UserId, Guid.NewGuid(), now, now.AddDays(1));
            var expired = Invitation.Create(org, target.UserId, manager.UserId, OrganizationRole.Parent, now.AddDays(-2), now.AddDays(-1));
            var hidden = Invitation.Create(foreign, target.UserId, otherManager.UserId, OrganizationRole.Student, now, now.AddDays(1));
            db.Invitations.AddRange(pending, expired, hidden); await db.SaveChangesAsync(); pendingId = pending.Id; expiredId = expired.Id;
        }
        var route = "/api/v1/organization/invitations";
        var all = await SchoolPageAsync<SchoolSentInvitationDto>(m, route);
        Assert.Equal(2, all.TotalCount); Assert.All(all.Items, i => Assert.Equal(org, i.OrganizationId));
        var pendingRow = Assert.Single(all.Items, i => i.InvitationId == pendingId);
        Assert.Equal(target.Username, pendingRow.TargetUsername); Assert.NotEmpty(pendingRow.TargetDisplayName);
        Assert.Equal(8, Convert.FromBase64String(pendingRow.Version).Length); Assert.Contains("Revoke", pendingRow.AllowedActions);
        Assert.Equal(InvitationStatus.Expired, all.Items.Single(i => i.InvitationId == expiredId).Status);
        Assert.Equal(pendingId, Assert.Single((await SchoolPageAsync<SchoolSentInvitationDto>(m,
            route + "?status=Pending&invitationType=PersonLink&pageSize=1")).Items).InvitationId);
        Assert.Equal(expiredId, Assert.Single((await SchoolPageAsync<SchoolSentInvitationDto>(m, route + "?status=Expired")).Items).InvitationId);
        var first = await SchoolPageAsync<SchoolSentInvitationDto>(m, route + "?pageSize=1");
        var second = await SchoolPageAsync<SchoolSentInvitationDto>(m, route + "?pageSize=1&page=2");
        Assert.NotEqual(first.Items.Single().InvitationId, second.Items.Single().InvitationId);
        Assert.Equal(all.Items.Select(i => i.InvitationId), (await SchoolPageAsync<SchoolSentInvitationDto>(m, route)).Items.Select(i => i.InvitationId));
        Assert.Single((await SchoolPageAsync<SchoolSentInvitationDto>(other, route)).Items);
        var json = await m.GetStringAsync(route); Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        await ErrorAsync(await t.GetAsync(route), HttpStatusCode.Forbidden, "WORKSPACE_NOT_ALLOWED");
        foreach (var query in new[] { "pageSize=101", "status=999", "invitationType=999" })
        { using var bad = await m.GetAsync(route + "?" + query); Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); }
    }
}
