using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ZibalPaymentEndpointsTests
{
    [Fact]
    public async Task ClosedAppAndLostCallbackRecoverWithNewAuthenticatedSessionAndDoNotGrantRoles()
    {
        var phone = $"+98936{Interlocked.Increment(ref _phoneSequence):D7}";
        var stub = new ZibalStub { AlreadyVerified = true };
        using var host = Host(stub: stub);
        var firstClient = Client(host);
        var setup = await SetupAsync(firstClient, personal: true, existingPhone: phone);
        firstClient.Dispose();
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddSeconds(1));

        using var resumed = Client(host);
        var userId = await AuthenticateAsync(resumed, phone);
        var before = await ReadAsync<PaymentAttemptDetailsDto>(await resumed.GetAsync(AttemptRoute(setup.Attempt)));
        Assert.Equal(MakanApp.Domain.Commerce.OrderStatus.PendingPayment, before.Order.Status);
        var recovered = await ReadAsync<VerifyPaymentResult>(await resumed.PostAsync(VerifyRoute(setup.Attempt), null));
        Assert.Equal(PaymentVerificationStatus.Settled, recovered.Status);
        Assert.Contains("/v1/inquiry", stub.Paths);
        await factory.DrainNotificationOutboxAsync();
        await factory.DrainNotificationOutboxAsync();
        await AssertSettlementAsync(setup.Order.OrderId, 1);
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.OrderId));
        await WithDb(async db =>
        {
            Assert.True(await db.UserSessions.CountAsync(s => s.UserId == userId) >= 2);
            Assert.Equal(0, await db.Memberships.CountAsync(m => m.UserId == userId));
            Assert.Equal(0, await db.PlatformRoleAssignments.CountAsync(r => r.UserId == userId));
        });
    }

    [Fact]
    public async Task TwoConcurrentZibalRecoveriesReturnOneSettledAndOneAlreadySettledAndOneFulfillment()
    {
        using var host = Host(); using var client = Client(host);
        var setup = await SetupAsync(client);
        var replies = await Task.WhenAll(client.PostAsync(VerifyRoute(setup.Attempt), null), client.PostAsync(VerifyRoute(setup.Attempt), null));
        var results = new[] { await ReadAsync<VerifyPaymentResult>(replies[0]), await ReadAsync<VerifyPaymentResult>(replies[1]) };
        Assert.Single(results, r => r.Status == PaymentVerificationStatus.Settled);
        Assert.Single(results, r => r.Status == PaymentVerificationStatus.AlreadySettled);
        await AssertSettlementAsync(setup.Order.OrderId, 1);
        await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.OrderId));
    }

    [Fact]
    public async Task ReusedZibalTrackIdIsRejectedAndCannotSettleSecondOrder()
    {
        var stub = new ZibalStub { CreationReferenceOverride = ZibalStub.ReserveTrackId() };
        using var host = Host(stub: stub); using var client = Client(host);
        var setup = await SetupAsync(client);
        var second = await ReadAsync<CreateOrderResult>(await client.PostAsJsonAsync("/api/v1/commerce/orders",
            new CreateOrderCommand(setup.Order.PlanVersionId, Guid.NewGuid()), JsonOptions));
        _ = await ReadAsync<VerifyPaymentResult>(await client.PostAsync(VerifyRoute(setup.Attempt), null));
        using var conflict = await client.PostAsJsonAsync($"/api/v1/commerce/orders/{second.Order.OrderId}/payments",
            new InitiatePaymentCommand(Guid.NewGuid()), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains(CommerceErrorCodes.PaymentReferenceConflict, await conflict.Content.ReadAsStringAsync());
        var details = await ReadAsync<OrderDto>(await client.GetAsync($"/api/v1/commerce/orders/{second.Order.OrderId}"));
        Assert.Equal(MakanApp.Domain.Commerce.OrderStatus.PendingPayment, details.Status);
        await AssertSettlementAsync(second.Order.OrderId, 0);
        await AssertSettlementAsync(setup.Order.OrderId, 1);
        await factory.DrainNotificationOutboxAsync();
        Assert.Equal(1, await factory.CountSubscriptionsForOrderAsync(setup.Order.OrderId));
    }
}
