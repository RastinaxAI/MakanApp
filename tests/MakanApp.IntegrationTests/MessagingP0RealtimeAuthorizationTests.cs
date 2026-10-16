using System.Net;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("suspended")]
    [InlineData("absolute-expired")]
    [InlineData("revoked")]
    public async Task RealtimeAudienceRechecksAccountAndBothSessionExpiries(string state)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await using var audienceScope = factory.Services.CreateAsyncScope();
        var resolver = audienceScope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>();
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var notification = await ReadP0ConversationNotificationAsync(setup.FirstClient, setup.ConversationId);
        var before = await resolver.GetActiveSessionIdsAsync(notification, setup.Second.UserId, now, default);
        var sessionId = Assert.Single(before);
        await using (var mutationScope = factory.Services.CreateAsyncScope())
        {
            var db = mutationScope.ServiceProvider.GetRequiredService<MakanDbContext>();
            if (state == "suspended")
                (await db.Users.SingleAsync(u => u.Id == setup.Second.UserId)).Suspend(now);
            else
            {
                var session = await db.UserSessions.SingleAsync(s => s.Id == sessionId);
                if (state == "revoked") session.Revoke(now);
                else db.Entry(session).Property(s => s.AbsoluteExpiresAtUtc).CurrentValue = now.AddSeconds(-1);
            }
            await db.SaveChangesAsync();
        }
        Assert.Empty(await resolver.GetActiveSessionIdsAsync(notification, setup.Second.UserId, now, default));
        Assert.NotEmpty(await resolver.GetActiveSessionIdsAsync(notification, setup.First.UserId, now, default));
        Assert.Equal(HttpStatusCode.Unauthorized, (await setup.SecondClient.GetAsync($"/api/v1/conversations/{setup.ConversationId}/messages")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealtimeAudienceUsesCurrentSelectedRoleAndOrganizationLikeRest(bool suspendOrganization)
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var manager = await factory.CreateMembershipAsync(owner.UserId, organizationId, OrganizationRole.Manager);
        var teacher = await factory.CreateMembershipAsync(member.UserId, organizationId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(ownerClient, manager.MembershipId, OrganizationRole.Manager);
        await SelectOrganizationAsync(memberClient, teacher.MembershipId, OrganizationRole.Teacher);
        var conversation = await StartDirectAndReadAsync(ownerClient, member.UserId, MakanApp.Domain.Messaging.ConversationScope.Organization);
        await using var scope = factory.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IMessagingRealtimeAudienceResolver>();
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var notification = await ReadP0ConversationNotificationAsync(ownerClient, conversation.ConversationId);
        Assert.Single(await resolver.GetActiveSessionIdsAsync(notification, member.UserId, now, default));
        if (suspendOrganization)
        {
            await using var mutationScope = factory.Services.CreateAsyncScope();
            var db = mutationScope.ServiceProvider.GetRequiredService<MakanDbContext>();
            (await db.Organizations.SingleAsync(o => o.Id == organizationId)).Suspend();
            await db.SaveChangesAsync();
        }
        else await factory.EndRoleAssignmentAsync(teacher.RoleAssignmentId);
        Assert.Empty(await resolver.GetActiveSessionIdsAsync(notification, member.UserId, now, default));
        Assert.Equal(HttpStatusCode.Forbidden, (await memberClient.GetAsync($"/api/v1/conversations/{conversation.ConversationId}/messages")).StatusCode);
    }
}
