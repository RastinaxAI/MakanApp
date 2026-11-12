using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Notification;
using MakanApp.Application.Organization;
using MakanApp.Application.Support;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task ProductV6SupportNotificationIsPostCommitRetrySafeAndNeverContainsPrivateMessage()
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var other = await ActorAsync();
        const string privateMarker = "SYNTHETIC_PRIVATE_SUPPORT_CONTENT";
        var ticket = await PostAsync<SupportTicketResult>(owner.Client, "/api/v1/support/tickets",
            new CreateSupportTicketCommand(Guid.NewGuid(), "تست اعلان پرونده", "General", "متن اولیه"));
        var reply = new ReplySupportTicketCommand(Guid.NewGuid(), ticket.Version, privateMarker);
        var replied = await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{ticket.Id}/messages", reply);
        await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{ticket.Id}/messages", reply);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var notification = await WithDb(async db => Assert.Single(await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == owner.UserId && n.Type == NotificationType.SupportUpdated && n.ResourceId == ticket.Id).ToArrayAsync()));
        Assert.DoesNotContain(privateMarker, notification.Body);
        var target = await GetAsync<NotificationTargetResult>(owner.Client, $"/api/v1/notifications/{notification.Id}/target");
        Assert.Equal($"/api/v1/support/tickets/{ticket.Id}", target.ApiRoute);
        using var hidden = await other.Client.GetAsync($"/api/v1/notifications/{notification.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        await WithDb(async db => Assert.False(await db.OutboxMessages.AnyAsync(o => o.MessageType == "notification.support-updated.v1" && o.Payload.Contains(privateMarker))));
        var assigned = await PostAsync<SupportTicketResult>(admin.Client, $"/api/admin/v1/support/tickets/{ticket.Id}/assign",
            new ManageSupportTicketCommand(Guid.NewGuid(), replied.Version, "پاسخ‌گویی مسئول پرونده", admin.UserId));
        var ownerReply = new ReplySupportTicketCommand(Guid.NewGuid(), assigned.Version, privateMarker);
        await PostAsync<SupportTicketResult>(owner.Client, $"/api/v1/support/tickets/{ticket.Id}/messages", ownerReply);
        await PostAsync<SupportTicketResult>(owner.Client, $"/api/v1/support/tickets/{ticket.Id}/messages", ownerReply);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var staffNotice = await WithDb(async db => Assert.Single(await db.Notifications.AsNoTracking()
            .Where(n => n.RecipientUserId == admin.UserId && n.Type == NotificationType.SupportUpdated && n.ResourceId == ticket.Id).ToArrayAsync()));
        Assert.DoesNotContain(privateMarker, staffNotice.Body);
    }

    [Fact]
    public async Task ProductV6InvitationOutboxRollsBackWithOwnerTransactionAndOldNotificationCannotAcceptExpiredInvitation()
    {
        var manager = await ActorAsync(); var targetUser = await ActorAsync(); var org = await factory.CreateOrganizationAsync("آزمون اعلان دعوت");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var rolledBack = Invitation.Create(org, targetUser.UserId, manager.UserId, OrganizationRole.Student, now, now.AddMinutes(1));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var people = scope.ServiceProvider.GetRequiredService<IOrganizationPersonStore>();
            await using var tx = await people.BeginAsync(default); people.Add(rolledBack); await people.SaveAsync(default);
        }
        await WithDb(async db => Assert.False(await db.Invitations.AnyAsync(i => i.Id == rolledBack.Id)));
        var invitation = Invitation.Create(org, targetUser.UserId, manager.UserId, OrganizationRole.Student, now, now.AddMinutes(1));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var people = scope.ServiceProvider.GetRequiredService<IOrganizationPersonStore>();
            await using var tx = await people.BeginAsync(default); people.Add(invitation); await people.SaveAsync(default); await tx.CommitAsync(default);
        }
        await factory.DrainNotificationOutboxAsync();
        var notification = await WithDb(async db => Assert.Single(await db.Notifications.Where(n => n.RecipientUserId == targetUser.UserId && n.ResourceId == invitation.Id).ToArrayAsync()));
        var link = await GetAsync<NotificationTargetResult>(targetUser.Client, $"/api/v1/notifications/{notification.Id}/target"); Assert.Equal("/api/v1/invitations", link.ApiRoute);
        factory.Clock.SetUtcNow(now.AddMinutes(2));
        using var expired = await targetUser.Client.GetAsync($"/api/v1/notifications/{notification.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        using var accept = await targetUser.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/accept", null); Assert.Equal(HttpStatusCode.Gone, accept.StatusCode);
        await WithDb(async db => {
            Assert.False(await db.Memberships.AnyAsync(m => m.UserId == targetUser.UserId && m.OrganizationId == org));
            Assert.False(await db.Notifications.AnyAsync(n => n.ResourceId == rolledBack.Id));
        });
    }

    [Fact]
    public async Task ProductV6ConfirmedPaymentCreatesOneGenericOwnerNotificationAndExistingFulfillment()
    {
        var owner = await ActorAsync(); var other = await ActorAsync(); var version = await PlanAsync(SubscriptionOwnerType.Individual);
        var order = await PostAsync<CreateOrderResult>(owner.Client, "/api/v1/commerce/personal/orders", new CreateOrderCommand(version, Guid.NewGuid()));
        var attempt = await PayAsync(owner, order.Order.OrderId); await VerifyAsync(attempt.PaymentAttemptId); await VerifyAsync(attempt.PaymentAttemptId);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var notice = await WithDb(async db => Assert.Single(await db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == owner.UserId &&
            n.Type == NotificationType.OrderPaid && n.ResourceId == order.Order.OrderId).ToArrayAsync()));
        var target = await GetAsync<NotificationTargetResult>(owner.Client, $"/api/v1/notifications/{notice.Id}/target");
        Assert.Equal($"/api/v1/commerce/personal/orders/{order.Order.OrderId}", target.ApiRoute);
        using var unauthorized = await other.Client.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, unauthorized.StatusCode);
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(order.Order.OrderId));
    }
}
