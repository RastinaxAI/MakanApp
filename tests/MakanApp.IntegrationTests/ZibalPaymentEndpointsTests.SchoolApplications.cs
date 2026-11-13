using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ZibalPaymentEndpointsTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SchoolApplicationZibalVerifiedPaymentPreservesPaidOrderAcrossProvisioningFailureAndRetry(bool failOnce)
    {
        await factory.DrainNotificationOutboxAsync();
        var stub = new ZibalStub(); using var host = Host(stub: stub, failActivationOnce: failOnce);
        using var owner = Client(host); using var admin = Client(host);
        var ownerId = await AuthenticateAsync(owner); var adminId = await AuthenticateAsync(admin);
        using var profile = await owner.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("School", "Manager", null, "zschool." + Guid.NewGuid().ToString("N")[..20]), JsonOptions);
        profile.EnsureSuccessStatusCode();
        await factory.ResetPlatformIamAsync(); await factory.BootstrapPlatformOwnerAsync(adminId);
        using var form = new MultipartFormDataContent();
        var bytes = new ByteArrayContent("synthetic school license"u8.ToArray()); bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(bytes, "file", "school-license-test.txt");
        var file = await ReadAsync<FileAssetResult>(await owner.PostAsync("/api/v1/files?workspaceScope=Personal", form));
        var app = await ReadAsync<SchoolApplicationResult>(await owner.PostAsJsonAsync("/api/v1/school-applications",
            new CreateSchoolApplicationCommand(Guid.NewGuid(), SchoolApplicationKind.LegalOrganization,
                new("مدرسه آزمون زیبال", "مؤسسه ساختگی", null, null, null, 0, 0), [new(file.Id, "License")]), JsonOptions));
        app = await ReadAsync<SchoolApplicationResult>(await owner.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/submit", new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version), JsonOptions));
        app = await ReadAsync<SchoolApplicationResult>(await admin.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/review", new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, "شروع بررسی آزمون"), JsonOptions));
        app = await ReadAsync<SchoolApplicationResult>(await admin.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/decisions", new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, "تأیید آزمون", SchoolApplicationStatus.Approved, ownerId), JsonOptions));
        Guid planVersionId = default; long mappingVersion = 0;
        await WithDb(async db => {
            var now = factory.Clock.GetUtcNow().UtcDateTime;
            var plan = Plan.Create("school-zibal." + Guid.NewGuid().ToString("N"), "خدمت تأسیس ساختگی تست", now, SubscriptionOwnerType.Organization);
            plan.SetAvailability(true, true, true, now);
            var version = PlanVersion.CreateDraft(plan.Id, 1, now); version.SetMonthlyTerms(BillingCalendar.Gregorian, "test-only-school-sale", null, null);
            version.SetPrice(31000, CurrencyCode.Irr); version.SetEntitlement(EntitlementKey.Academic, true);
            version.SetQuota(QuotaKey.ActiveClasses, 1); version.SetQuota(QuotaKey.ActiveLearners, 0); version.Publish(now);
            db.Plans.Add(plan); db.PlanVersions.Add(version); await db.SaveChangesAsync(); planVersionId = version.Id;
            mappingVersion = await db.SchoolEstablishmentOfferings.Where(o => o.Kind == app.Kind).Select(o => (long?)o.Version).SingleOrDefaultAsync() ?? 0;
        });
        _ = await ReadAsync<SchoolOfferingResult>(await admin.PutAsJsonAsync($"/api/admin/v1/school-applications/offerings/{app.Kind}",
            new SchoolOfferingCommand(Guid.NewGuid(), mappingVersion, planVersionId, "پیکربندی ساختگی تست زیبال"), JsonOptions));
        // مبلغ و PlanVersion ارسالی اضافی هیچ اثری بر مبلغ مرجع سرور ندارند.
        var created = await ReadAsync<CreateOrderResult>(await owner.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/orders",
            new { clientOperationId = Guid.NewGuid(), expectedVersion = app.Version, amount = 1, planVersionId = Guid.NewGuid() }, JsonOptions));
        Assert.Equal(31000, created.Order.Amount); Assert.Equal(planVersionId, created.Order.PlanVersionId); Assert.Equal(app.Id, created.Order.SchoolApplicationId);
        var attempt = await ReadAsync<InitiatePaymentResult>(await owner.PostAsJsonAsync($"/api/v1/commerce/personal/orders/{created.Order.OrderId}/payments", new InitiatePaymentCommand(Guid.NewGuid()), JsonOptions));
        _activationFailureTarget = created.Order.OrderId;
        for (var index = 0; index < 10; index++)
        {
            using var callback = await owner.GetAsync(Callback(attempt.PaymentAttempt));
            Assert.Equal(HttpStatusCode.SeeOther, callback.StatusCode);
        }
        var verifications = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => owner.PostAsync(VerifyRoute(attempt.PaymentAttempt), null)));
        foreach (var verification in verifications) { verification.EnsureSuccessStatusCode(); verification.Dispose(); }
        var receipt = await ReadAsync<PaymentReceiptDto>(await owner.GetAsync($"/api/v1/commerce/personal/orders/{created.Order.OrderId}/receipt"));
        Assert.Equal(31000, receipt.Amount); Assert.Equal(OrderFulfillmentState.ProvisioningPending, receipt.FulfillmentState);
        await using (var scope = host.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Outbox.OutboxProcessor>().DispatchBatchAsync(default);
        if (failOnce)
        {
            await WithDb(async db => {
                Assert.False(await db.PlatformProvisioningRequests.AnyAsync(p => p.SourceOrderId == created.Order.OrderId));
                Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync(o => o.Id == created.Order.OrderId)).Status);
            });
            factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(10));
        }
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var recovered = await ReadAsync<VerifyPaymentResult>(await owner.PostAsync(VerifyRoute(attempt.PaymentAttempt), null));
        Assert.Equal(PaymentVerificationStatus.AlreadySettled, recovered.Status);
        Assert.Equal(1, stub.RequestCount); Assert.Equal(1, stub.VerifyCount);
        await WithDb(async db => {
            var intent = Assert.Single(await db.PlatformProvisioningRequests.Where(p => p.SchoolApplicationId == app.Id).ToArrayAsync());
            Assert.Equal(created.Order.OrderId, intent.SourceOrderId); Assert.Null(intent.FailureCode);
            Assert.NotNull(intent.OrganizationId); Assert.Equal(ownerId, intent.InitialManagerUserId);
            Assert.Equal(MakanApp.Domain.Platform.PlatformProvisioningStatus.Completed, intent.Status);
            Assert.NotNull(intent.ManagerInvitationId); Assert.NotNull(intent.SourcePaymentId);
            Assert.Equal(1, await db.Payments.CountAsync(p => p.OrderId == created.Order.OrderId));
            Assert.Equal(1, await db.Subscriptions.CountAsync(s => s.SourceOrderId == created.Order.OrderId));
            Assert.False(await db.Memberships.AnyAsync(m => m.UserId == ownerId));
        });
    }
}
