using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Notification;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Notification;
using MakanApp.Infrastructure.Notification;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Theory]
    [InlineData("groups", "accept")]
    [InlineData("channels", "accept")]
    [InlineData("groups", "block")]
    [InlineData("channels", "expire")]
    [InlineData("groups", "privacy")]
    [InlineData("channels", "ban")]
    public async Task Be16InitialMessagingInvitationIsDurableRetrySafeAndRechecksCurrentState(string resource, string transition)
    {
        using var setup = await CreateP2ManagedSetupAsync(resource);
        var command = PersonalCommand("آزمون اعلان دعوت", setup.Target.UserId);
        using var first = await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{resource}", command, JsonOptions); first.EnsureSuccessStatusCode();
        var created = (await first.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        using var replay = await setup.OwnerClient.PostAsJsonAsync($"/api/v1/conversations/{resource}", command, JsonOptions); replay.EnsureSuccessStatusCode();
        var repeated = (await replay.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
        var invitation = Assert.Single(created.InitialTargets); Assert.Equal(invitation.InvitationId, Assert.Single(repeated.InitialTargets).InvitationId);
        var notice = await Be16MessagingNoticeAsync(setup.Target.UserId, NotificationType.ConversationInvitation, invitation.InvitationId!.Value);
        using var target = await setup.TargetClient.GetAsync($"/api/v1/notifications/{notice.Id}/target"); target.EnsureSuccessStatusCode();
        Assert.Equal("/api/v1/messaging/invitations", (await target.Content.ReadFromJsonAsync<NotificationTargetResult>(JsonOptions))!.ApiRoute);
        using var hidden = await setup.OwnerClient.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        try
        {
            if (transition == "accept")
            {
                using var accept = await setup.TargetClient.PostAsync($"/api/v1/messaging/invitations/{invitation.InvitationId}/accept", null); accept.EnsureSuccessStatusCode();
            }
            else if (transition == "block")
            {
                using var block = await setup.TargetClient.PostAsync($"/api/v1/messaging/blocks/{setup.Owner.UserId}", null); block.EnsureSuccessStatusCode();
            }
            else if (transition == "privacy")
            {
                var privacy = (await setup.TargetClient.GetFromJsonAsync<MessagingPrivacySettingsResult>("/api/v1/messaging/privacy", JsonOptions))!;
                using var changed = await setup.TargetClient.PutAsJsonAsync("/api/v1/messaging/privacy", new UpdateMessagingPrivacyCommand(privacy.DirectMessagesAudience,
                    privacy.PhoneNumberVisibilityAudience, privacy.PhoneNumberDiscoveryAudience, PrivacyAudience.Nobody, privacy.Version), JsonOptions);
                changed.EnsureSuccessStatusCode();
            }
            else if (transition == "ban")
            {
                await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                var restriction = ConversationMemberRestriction.Create(created.ConversationId, setup.Target.UserId);
                restriction.Update(true, GroupMemberCapability.None, null, now); db.Add(restriction); await db.SaveChangesAsync();
            }
            else
            {
                await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                var expiring = await db.ConversationInvitations.SingleAsync(i => i.Id == invitation.InvitationId);
                db.Entry(expiring).Property(i => i.ExpiresAtUtc).CurrentValue = now.AddMinutes(1); await db.SaveChangesAsync();
                factory.Clock.SetUtcNow(now.AddMinutes(2));
            }
            using var stale = await setup.TargetClient.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
        }
        finally { factory.Clock.UseSystemTime(); }
    }

    [Theory]
    [InlineData("groups", "approve")]
    [InlineData("groups", "reject")]
    [InlineData("channels", "approve")]
    [InlineData("channels", "reject")]
    public async Task Be16MessagingJoinRequestAndDecisionNotifyOnlyTheirCurrentAudience(string resource, string decision)
    {
        using var setup = await CreateP2ManagedSetupAsync(resource);
        var link = await CreateP2LinkAsync(setup.OwnerClient, setup.Conversation.ConversationId, P2LinkCommand() with { RequiresApproval = true });
        var requested = (await ReadP2RedemptionAsync(setup.TargetClient, link)).JoinRequest!;
        Assert.Equal(requested.Id, (await ReadP2RedemptionAsync(setup.TargetClient, link)).JoinRequest!.Id);
        var managerNotice = await Be16MessagingNoticeAsync(setup.Owner.UserId, NotificationType.ConversationJoinRequestUpdated, requested.Id);
        using var managerTarget = await setup.OwnerClient.GetAsync($"/api/v1/notifications/{managerNotice.Id}/target"); managerTarget.EnsureSuccessStatusCode();
        Assert.Equal($"/api/v1/conversations/{setup.Conversation.ConversationId}/join-requests", (await managerTarget.Content.ReadFromJsonAsync<NotificationTargetResult>(JsonOptions))!.ApiRoute);
        using var completed = await CompleteP2RequestAsync(setup.OwnerClient, requested, decision); completed.EnsureSuccessStatusCode();
        using var replay = await CompleteP2RequestAsync(setup.OwnerClient, requested, decision); replay.EnsureSuccessStatusCode();
        var requesterNotice = await Be16MessagingNoticeAsync(setup.Target.UserId, NotificationType.ConversationJoinRequestUpdated, requested.Id);
        using var requesterTarget = await setup.TargetClient.GetAsync($"/api/v1/notifications/{requesterNotice.Id}/target"); requesterTarget.EnsureSuccessStatusCode();
        Assert.Equal("/api/v1/messaging/join-requests", (await requesterTarget.Content.ReadFromJsonAsync<NotificationTargetResult>(JsonOptions))!.ApiRoute);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Be16MessagingInvitationAndJoinOutboxRollbackWithOwnerTransaction(bool join)
    {
        using var setup = await CreateP2ManagedSetupAsync("groups"); var now = factory.Clock.GetUtcNow().UtcDateTime; Guid source;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); await using var tx = await db.Database.BeginTransactionAsync();
            if (join)
            {
                var link = ConversationInviteLink.Create(Guid.NewGuid(), setup.Conversation.ConversationId, setup.Owner.UserId, new string('A', 64), now.AddDays(1), 10, true, now);
                db.Add(link);
                var request = ConversationJoinRequest.Create(link.ConversationId, link.Id, setup.Target.UserId, link.ExpiresAtUtc, now); db.Add(request); source = request.Id;
            }
            else
            {
                var invitation = ConversationInvitation.Create(setup.Conversation.ConversationId, setup.Owner.UserId, setup.Target.UserId, now.AddDays(1), now);
                db.Add(invitation); source = invitation.Id;
            }
            await scope.ServiceProvider.GetRequiredService<ProductNotificationWriter>().CaptureMessagingInvitationsAsync(default); await db.SaveChangesAsync();
            Assert.True(await db.OutboxMessages.AnyAsync(o => o.MessageType.StartsWith("notification.conversation-") && o.Payload.Contains(source.ToString())));
            await tx.RollbackAsync();
        }
        await factory.DrainNotificationOutboxAsync();
        await using var check = factory.Services.CreateAsyncScope(); var persisted = check.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.False(await persisted.Notifications.AnyAsync(n => n.ResourceId == source));
        Assert.False(await persisted.OutboxMessages.AnyAsync(o => o.Payload.Contains(source.ToString())));
    }

    private async Task<MakanApp.Domain.Notification.Notification> Be16MessagingNoticeAsync(Guid user, NotificationType type, Guid resource)
    {
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return Assert.Single(await db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == user && n.Type == type && n.ResourceId == resource).ToArrayAsync());
    }
}
