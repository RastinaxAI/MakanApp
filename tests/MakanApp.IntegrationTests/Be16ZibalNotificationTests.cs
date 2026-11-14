using System.Net;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ZibalPaymentEndpointsTests
{
    [Theory]
    [InlineData("timeout")]
    [InlineData("amount")]
    public async Task Be16CallbackWithoutValidServerProofNeverCreatesPaidNotification(string failure)
    {
        using var host = Host(stub: new ZibalStub { Failure = failure }); using var client = Client(host);
        var setup = await SetupAsync(client); using var callback = await client.GetAsync(Callback(setup.Attempt)); Assert.Equal(HttpStatusCode.SeeOther, callback.StatusCode);
        await factory.DrainNotificationOutboxAsync();
        await WithDb(async db => {
            Assert.False(await db.Notifications.AnyAsync(n => n.ResourceId == setup.Order.OrderId && n.Type == NotificationType.OrderPaid));
            Assert.False(await db.OutboxMessages.AnyAsync(o => o.MessageType == "notification.order-paid.v1" && o.Payload.Contains(setup.Order.OrderId.ToString())));
        });
        await AssertSettlementAsync(setup.Order.OrderId, 0);
    }

    [Fact]
    public async Task Be16ZibalVerifiedCallbackAndRepeatedVerifyProduceOneGenericPaidReceipt()
    {
        using var host = Host(); using var client = Client(host); var setup = await SetupAsync(client);
        await WithDb(async db => Assert.False(await db.Notifications.AnyAsync(n => n.ResourceId == setup.Order.OrderId)));
        using var callback = await client.GetAsync(Callback(setup.Attempt)); Assert.Equal(HttpStatusCode.SeeOther, callback.StatusCode);
        for (var i = 0; i < 2; i++) { using var verified = await client.PostAsync(VerifyRoute(setup.Attempt), null); verified.EnsureSuccessStatusCode(); }
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        Guid notificationId = Guid.Empty;
        await WithDb(async db => {
            var receipt = Assert.Single(await db.Notifications.Where(n => n.ResourceId == setup.Order.OrderId && n.Type == NotificationType.OrderPaid).ToArrayAsync());
            notificationId = receipt.Id; Assert.DoesNotContain(setup.Attempt.ProviderReference!, receipt.Body); Assert.DoesNotContain("777123456", receipt.Body);
        });
        var target = await ReadAsync<NotificationTargetResult>(await client.GetAsync($"/api/v1/notifications/{notificationId}/target"));
        Assert.Equal($"/api/v1/commerce/orders/{setup.Order.OrderId}", target.ApiRoute);
        await WithDb(async db => { (await db.Memberships.SingleAsync(m => m.Id == setup.MembershipId)).End(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        using var revoked = await client.GetAsync($"/api/v1/notifications/{notificationId}/target"); Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        await AssertSettlementAsync(setup.Order.OrderId, 1);
    }
}
