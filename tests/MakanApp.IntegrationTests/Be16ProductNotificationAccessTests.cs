using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Notification;
using MakanApp.Application.Organization;
using MakanApp.Application.Support;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData("accepted")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task Be16OrganizationInvitationTargetAndQueuedPushRequirePendingUnexpiredState(string transition)
    {
        var inviter = await ActorAsync(); var target = await ActorAsync(); var org = await factory.CreateOrganizationAsync("چرخهٔ دعوت اعلان سازمانی");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        await PostAsync<PushDeviceRegistrationResult>(target.Client, "/api/v1/notifications/devices", new RegisterPushDeviceCommand(PushDevicePlatform.Android, "be16-" + Guid.NewGuid().ToString("N")));
        try
        {
            var invitation = Invitation.Create(org, target.UserId, inviter.UserId, OrganizationRole.Student, now, now.AddMinutes(1));
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var store = scope.ServiceProvider.GetRequiredService<IOrganizationPersonStore>();
                await using var tx = await store.BeginAsync(default); store.Add(invitation); await store.SaveAsync(default); await tx.CommitAsync(default);
            }
            var notice = await Be16NoticeAsync(target.UserId, NotificationType.OrganizationInvitation, invitation.Id);
            Assert.Equal("/api/v1/invitations", (await GetAsync<NotificationTargetResult>(target.Client, $"/api/v1/notifications/{notice.Id}/target")).ApiRoute);
            if (transition == "accepted")
            {
                using var accepted = await target.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/accept", null); accepted.EnsureSuccessStatusCode();
            }
            else if (transition == "revoked")
                await WithDb(async db => { (await db.Invitations.SingleAsync(i => i.Id == invitation.Id)).Revoke(now); await db.SaveChangesAsync(); });
            else factory.Clock.SetUtcNow(now.AddMinutes(2));
            using var stale = await target.Client.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
            factory.NotificationDeliveryProvider.Reset(); await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(NotificationDeliveryFailureReason.ResourceAccessRevoked, Assert.Single(await factory.GetDeliveryAttemptsAsync(notice.Id), a => a.Channel == NotificationDeliveryChannel.Push).FailureReason);
            Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, p => p.NotificationId == notice.Id);
        }
        finally { await Be16RevokeDevicesAsync(target.UserId); }
    }

    [Fact]
    public async Task Be16SchoolReviewIsPostCommitGenericRetrySafeAndUsesOwnerRoute()
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var other = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        await factory.DrainNotificationOutboxAsync();
        await WithDb(async db => Assert.False(await db.Notifications.AnyAsync(n => n.ResourceId == app.Id)));
        var submitted = await SchoolAction(owner, app, "submit");
        var operation = Guid.NewGuid(); const string privateReason = "SYNTHETIC_BE16_PRIVATE_SCHOOL_REVIEW";
        var command = new SchoolApplicationActionCommand(operation, submitted.Version, privateReason);
        await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/review", command);
        await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/review", command);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var notices = await WithDb(db => db.Notifications.Where(n => n.RecipientUserId == owner.UserId && n.ResourceId == app.Id &&
            n.Type == NotificationType.SchoolApplicationUpdated).OrderByDescending(n => n.CreatedAtUtc).ThenBy(n => n.Id).ToArrayAsync());
        Assert.Equal(2, notices.Length); var notice = notices[0];
        // Submit و Review دو رویداد واقعی‌اند؛ تکرار Review رویداد سوم نمی‌سازد.
        await WithDb(async db => {
            Assert.Equal(2, await db.Notifications.CountAsync(n => n.RecipientUserId == owner.UserId && n.ResourceId == app.Id));
            Assert.False(await db.Notifications.AnyAsync(n => n.ResourceId == app.Id && n.Body.Contains(privateReason)));
            Assert.False(await db.OutboxMessages.AnyAsync(o => o.MessageType == "notification.school-application-updated.v1" && o.Payload.Contains(privateReason)));
        });
        Assert.Equal($"/api/v1/school-applications/{app.Id}", (await GetAsync<NotificationTargetResult>(owner.Client, $"/api/v1/notifications/{notice.Id}/target")).ApiRoute);
        using var concealed = await other.Client.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, concealed.StatusCode);
    }

    [Fact]
    public async Task Be16SupportStaffPermissionRevocationStopsTargetAndQueuedPushWithoutLeakingReply()
    {
        var staff = await AdminAsync(); var owner = await ActorAsync();
        var ticket = await PostAsync<SupportTicketResult>(owner.Client, "/api/v1/support/tickets", new CreateSupportTicketCommand(Guid.NewGuid(), "اعلان امن پشتیبانی", "Technical", "درخواست آزمایشی"));
        var assigned = await PostAsync<SupportTicketResult>(staff.Client, $"/api/admin/v1/support/tickets/{ticket.Id}/assign", new ManageSupportTicketCommand(Guid.NewGuid(), ticket.Version, "پاسخ‌گو", staff.UserId));
        await PostAsync<PushDeviceRegistrationResult>(staff.Client, "/api/v1/notifications/devices", new RegisterPushDeviceCommand(PushDevicePlatform.Android, "be16-" + Guid.NewGuid().ToString("N")));
        try
        {
            const string privateReply = "SYNTHETIC_BE16_PRIVATE_SUPPORT_REPLY";
            var command = new ReplySupportTicketCommand(Guid.NewGuid(), assigned.Version, privateReply);
            await PostAsync<SupportTicketResult>(owner.Client, $"/api/v1/support/tickets/{ticket.Id}/messages", command);
            await PostAsync<SupportTicketResult>(owner.Client, $"/api/v1/support/tickets/{ticket.Id}/messages", command);
            var notice = await Be16NoticeAsync(staff.UserId, NotificationType.SupportUpdated, ticket.Id);
            Assert.DoesNotContain(privateReply, notice.Body);
            Assert.Equal($"/api/admin/v1/support/tickets/{ticket.Id}", (await GetAsync<NotificationTargetResult>(staff.Client, $"/api/v1/notifications/{notice.Id}/target")).ApiRoute);
            await factory.ResetPlatformIamAsync();
            using var stale = await staff.Client.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
            factory.NotificationDeliveryProvider.Reset(); await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(NotificationDeliveryFailureReason.ResourceAccessRevoked, Assert.Single(await factory.GetDeliveryAttemptsAsync(notice.Id), a => a.Channel == NotificationDeliveryChannel.Push).FailureReason);
            Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, p => p.NotificationId == notice.Id);
            await WithDb(async db => Assert.False(await db.OutboxMessages.AnyAsync(o => o.MessageType == "notification.support-updated.v1" && o.Payload.Contains(privateReply))));
        }
        finally { await Be16RevokeDevicesAsync(staff.UserId); }
    }
}
