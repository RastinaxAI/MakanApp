using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Outbox;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task SchoolPaidRepairCanCreateMissingIntentAndActivationReplayCannotDuplicateIt()
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var app = await ApprovedSchoolAsync(applicant, admin);
        var order = await PaidSchoolOrderAsync(applicant, admin, app);
        await WithDb(async db => Assert.False(await db.PlatformProvisioningRequests.AnyAsync(p => p.SchoolApplicationId == app.Id)));
        var repaired = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/provisioning/retry",
            new SchoolProvisioningRetryCommand(Guid.NewGuid(), app.Version, "بازیابی پرداختی که Worker هنوز intent آن را ثبت نکرده است"));
        Assert.NotNull(repaired.Progress!.ProvisioningRequestId);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var ready = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Ready", ready.NextAction); Assert.Equal(repaired.Progress.ProvisioningRequestId, ready.Progress!.ProvisioningRequestId);
        await WithDb(async db => {
            Assert.Single(await db.PlatformProvisioningRequests.Where(p => p.SchoolApplicationId == app.Id).ToArrayAsync());
            Assert.Single(await db.Payments.Where(p => p.OrderId == order.Order.OrderId).ToArrayAsync());
            Assert.Single(await db.Subscriptions.Where(s => s.SourceOrderId == order.Order.OrderId).ToArrayAsync());
        });
    }

    [Fact]
    public async Task SchoolWorkCannotAdvanceAnUnrelatedPlatformProvisioningRequest()
    {
        var actor = await ActorAsync();
        var requestId = await WithDb(async db => {
            var request = PlatformProvisioningRequest.Create("test-general-" + Guid.NewGuid().ToString("N"), actor.UserId, "General provision", null, null, null, null,
                "درخواست عمومی ساختگی برای اثبات مرز Worker", factory.Clock.GetUtcNow().UtcDateTime);
            db.PlatformProvisioningRequests.Add(request); await db.SaveChangesAsync(); return request.Id;
        });
        await using var scope = factory.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ISchoolProvisioningWorkHandler>()
            .HandleAsync(JsonSerializer.Serialize(new { RequestId = requestId }), default));
        await WithDb(async db => {
            var saved = await db.PlatformProvisioningRequests.SingleAsync(r => r.Id == requestId);
            Assert.Equal(PlatformProvisioningStatus.Requested, saved.Status); Assert.Null(saved.OrganizationId); Assert.Null(saved.FailureCode);
        });
    }

    [Fact]
    public async Task SchoolLegacyPaidIntentIsCompletedByExplicitManagerSelectionWithoutRepaymentOrNewIntent()
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var manager = await ActorAsync(); await CompleteSchoolManagerAsync(manager);
        var app = await ApprovedSchoolAsync(applicant, admin); var order = await PaidSchoolOrderAsync(applicant, admin, app);
        var requestId = await WithDb(async db => {
            // فقط fixture ایزوله: وضعیت ستون‌های nullable تازه را مثل دادهٔ پیش از این Migration می‌سازیم.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[SchoolApplications] SET [FinalInitialManagerUserId]=NULL,[InitialManagerConfirmedAtUtc]=NULL,[InitialManagerConfirmedByUserId]=NULL WHERE [Id]={app.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[SchoolApplicationHistory] SET [ProposedInitialManagerUserId]=NULL,[FinalInitialManagerUserId]=NULL WHERE [ApplicationId]={app.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[SchoolApplicationDecisions] SET [ApplicationVersion]=NULL,[ProposedInitialManagerUserId]=NULL,[FinalInitialManagerUserId]=NULL WHERE [ApplicationId]={app.Id}");
            var request = PlatformProvisioningRequest.AwaitSchoolManagerPolicy(app.Id, order.Order.OrderId, applicant.UserId, app.Data.SchoolName!, order.Order.PlanVersionId, factory.Clock.GetUtcNow().UtcDateTime);
            db.PlatformProvisioningRequests.Add(request); await db.SaveChangesAsync(); return request.Id;
        });
        await factory.DrainNotificationOutboxAsync();
        var blocked = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Paid", blocked.Progress!.PaymentState); Assert.Equal("ContactSupportForProvisioning", blocked.NextAction);
        Assert.Equal(requestId, blocked.Progress.ProvisioningRequestId); Assert.Null(blocked.FinalInitialManagerUserId);
        var command = new SchoolInitialManagerCommand(Guid.NewGuid(), blocked.Version, manager.UserId, "انتخاب صریح مدیر برای پرداخت قدیمی محفوظ");
        var selected = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/initial-manager/confirm", command);
        var duplicate = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/initial-manager/confirm", command);
        Assert.Equal(selected.Version, duplicate.Version);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var ready = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Ready", ready.NextAction); Assert.Equal(manager.UserId, ready.FinalInitialManagerUserId);
        Assert.Equal(requestId, ready.Progress!.ProvisioningRequestId); Assert.Equal(order.Order.OrderId, ready.Progress.OrderId);
        Assert.Equal(order.Order.PlanVersionId, ready.Progress.PlanVersionId);
        await WithDb(async db => {
            Assert.Single(await db.Orders.Where(o => o.SchoolApplicationId == app.Id).ToArrayAsync());
            Assert.Single(await db.Payments.Where(p => p.OrderId == order.Order.OrderId).ToArrayAsync());
            Assert.Single(await db.PlatformProvisioningRequests.Where(p => p.SchoolApplicationId == app.Id).ToArrayAsync());
            Assert.False(await db.Memberships.AnyAsync(m => m.OrganizationId == ready.Progress.OrganizationId));
        });
    }

    [Fact]
    public async Task SchoolLegacyApprovedSelectionCanBeExplicitlyCompletedOnceWithoutReplacingFinalManager()
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var manager = await ActorAsync(); await CompleteSchoolManagerAsync(manager);
        var app = await SchoolDraftAsync(applicant); app = await SchoolAction(applicant, app, "submit"); app = await SchoolAction(admin, app, "review", "بررسی دادهٔ قدیمی آزمون");
        await WithDb(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[SchoolApplications] SET [Status]={6},[Version]=[Version]+1 WHERE [Id]={app.Id}"));
        app = await GetAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}");
        Assert.Null(app.FinalInitialManagerUserId); Assert.Equal("WaitForInitialManagerConfirmation", app.NextAction);
        var command = new SchoolInitialManagerCommand(Guid.NewGuid(), app.Version, manager.UserId, "ثبت صریح انتخابِ خالی پروندهٔ قدیمی");
        var confirmed = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/initial-manager/confirm", command);
        var repeated = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/initial-manager/confirm", command);
        Assert.Equal(confirmed.Version, repeated.Version); Assert.Equal(manager.UserId, confirmed.FinalInitialManagerUserId);
        using var replacement = await admin.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/initial-manager/confirm",
            command with { ClientOperationId = Guid.NewGuid(), ExpectedVersion = confirmed.Version, InitialManagerUserId = applicant.UserId }, Json);
        Assert.Equal(HttpStatusCode.Conflict, replacement.StatusCode);
        await WithDb(async db => {
            Assert.Single(await db.SchoolApplicationHistories.Where(h => h.ApplicationId == app.Id && h.Action == "school_application.InitialManagerConfirmed").ToArrayAsync());
            Assert.False(await db.Memberships.AnyAsync(m => m.UserId == manager.UserId));
            Assert.False(await db.PlatformProvisioningRequests.AnyAsync(p => p.SchoolApplicationId == app.Id));
        });
    }

    [Theory]
    [InlineData("missing")] [InlineData("nonexistent")] [InlineData("incomplete")] [InlineData("suspended")]
    public async Task SchoolApprovalRejectsIneligibleManagerWithoutRecordingDecision(string kind)
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var manager = await ActorAsync();
        var app = await SchoolDraftAsync(applicant); app = await SchoolAction(applicant, app, "submit"); app = await SchoolAction(admin, app, "review", "بررسی آزمون");
        if (kind == "suspended")
        {
            await CompleteSchoolManagerAsync(manager);
            await WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == manager.UserId)).Suspend(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        }
        Guid? target = kind switch { "missing" => null, "nonexistent" => Guid.NewGuid(), _ => manager.UserId };
        using var response = await admin.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/decisions",
            new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, "انتخاب مدیر نامعتبر آزمون", SchoolApplicationStatus.Approved, target), Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal(SchoolApplicationErrorCodes.InitialManagerInvalid, await ProblemAsync(response));
        await WithDb(async db => {
            var saved = await db.SchoolApplications.SingleAsync(a => a.Id == app.Id);
            Assert.Equal(app.Version, saved.Version); Assert.Null(saved.FinalInitialManagerUserId);
            Assert.Equal(1, await db.SchoolApplicationDecisions.CountAsync(d => d.ApplicationId == app.Id));
        });
    }

    [Fact]
    public async Task SchoolAlternateManagerReceivesOnlyInvitationAndApplicantGetsNoAutomaticMembership()
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var manager = await ActorAsync(); await CompleteSchoolManagerAsync(manager);
        var app = await SchoolDraftAsync(applicant); Assert.Equal(applicant.UserId, app.ProposedInitialManagerUserId); Assert.Null(app.FinalInitialManagerUserId);
        app = await SchoolAction(applicant, app, "submit"); app = await SchoolAction(admin, app, "review", "شروع بررسی آزمون");
        app = await SchoolAction(admin, app, "decisions", "مدیر جایگزین صریح آزمون", SchoolApplicationStatus.Approved, manager.UserId);
        var order = await PaidSchoolOrderAsync(applicant, admin, app);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var ready = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Ready", ready.NextAction); Assert.Equal(manager.UserId, ready.FinalInitialManagerUserId);
        var orders = await GetAsync<OrderListResult>(applicant.Client, "/api/v1/commerce/personal/orders");
        Assert.Equal(OrderFulfillmentState.ProvisioningCompleted, orders.Items.Single(o => o.OrderId == order.Order.OrderId).FulfillmentState);
        var invitationId = ready.Progress!.ManagerInvitationId!.Value;
        await WithDb(async db => {
            var invitation = await db.Invitations.SingleAsync(i => i.Id == invitationId);
            Assert.Equal(manager.UserId, invitation.DestinationUserId); Assert.Equal(OrganizationRole.Manager, invitation.Role);
            Assert.Equal(invitation.CreatedAtUtc.AddDays(7), invitation.ExpiresAtUtc);
            Assert.False(await db.Memberships.AnyAsync(m => m.OrganizationId == ready.Progress.OrganizationId));
            var history = await db.SchoolApplicationHistories.SingleAsync(h => h.ApplicationId == app.Id && h.Action == "school_application.Approved");
            Assert.Equal(manager.UserId, history.FinalInitialManagerUserId); Assert.Equal(applicant.UserId, history.ProposedInitialManagerUserId);
            Assert.Equal(admin.UserId, history.ActorUserId);
        });
        using var wrong = await applicant.Client.PostAsync($"/api/v1/invitations/{invitationId}/accept", null); Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        using var accept = await manager.Client.PostAsync($"/api/v1/invitations/{invitationId}/accept", null); accept.EnsureSuccessStatusCode();
        using var retry = await manager.Client.PostAsync($"/api/v1/invitations/{invitationId}/accept", null); retry.EnsureSuccessStatusCode();
        await WithDb(async db => {
            var member = Assert.Single(await db.Memberships.Where(m => m.OrganizationId == ready.Progress.OrganizationId).ToArrayAsync());
            Assert.Equal(manager.UserId, member.UserId);
            Assert.Single(await db.RoleAssignments.Where(r => r.MembershipId == member.Id && r.Role == OrganizationRole.Manager).ToArrayAsync());
            Assert.Single(await db.Subscriptions.Where(s => s.SourceOrderId == order.Order.OrderId && s.CommercialControlEnabled).ToArrayAsync());
        });
    }

    [Fact]
    public async Task SchoolPaidFailureAfterSubscriptionIsAuditableAndRepairPreservesIdsAcrossConcurrentWorkers()
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var app = await ApprovedSchoolAsync(applicant, admin);
        var order = await PaidSchoolOrderAsync(applicant, admin, app); var requestId = await PrepareSchoolIntentAsync(order.Order.OrderId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var options = new DbContextOptionsBuilder<MakanDbContext>(scope.ServiceProvider.GetRequiredService<DbContextOptions<MakanDbContext>>())
                .AddInterceptors(new FailSchoolInvitationOnce()).Options;
            await using var db = new MakanDbContext(options, factory.Clock);
            var handler = new EfPlatformOperationsStore(db, factory.Clock, new EfOutbox(db), Options.Create(new SchoolProvisioningOptions()));
            await Assert.ThrowsAsync<DbUpdateException>(() => handler.HandleAsync(JsonSerializer.Serialize(new { RequestId = requestId }), default));
        }
        var failed = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Paid", failed.Progress!.PaymentState); Assert.Equal("NeedsAttention", failed.Progress.ProvisioningState);
        Assert.Equal("ContactSupportForProvisioning", failed.NextAction); Assert.NotNull(failed.Progress.OrganizationId); Assert.NotNull(failed.Progress.SubscriptionId);
        Assert.Null(failed.Progress.ManagerInvitationId); Assert.NotNull(failed.Progress.FailureCode);
        Assert.Equal(requestId, await PrepareSchoolIntentAsync(order.Order.OrderId));
        var unchangedFailure = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("NeedsAttention", unchangedFailure.Progress!.ProvisioningState);
        var receipt = await GetAsync<PaymentReceiptDto>(applicant.Client, $"/api/v1/commerce/personal/orders/{order.Order.OrderId}/receipt");
        Assert.Equal(OrderFulfillmentState.ProvisioningNeedsAttention, receipt.FulfillmentState);
        var orders = await GetAsync<OrderListResult>(applicant.Client, "/api/v1/commerce/personal/orders");
        Assert.Equal(OrderFulfillmentState.ProvisioningNeedsAttention, orders.Items.Single(o => o.OrderId == order.Order.OrderId).FulfillmentState);
        using var repayment = await applicant.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/orders", new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version), Json);
        Assert.Equal(HttpStatusCode.Conflict, repayment.StatusCode);
        var command = new SchoolProvisioningRetryCommand(Guid.NewGuid(), app.Version, "بازیابی شکست ساختگی دعوت");
        var repaired = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/provisioning/retry", command);
        var duplicate = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/provisioning/retry", command);
        Assert.Equal(repaired.Version, duplicate.Version);
        await Task.WhenAll(DispatchSchoolWorkerAsync(), DispatchSchoolWorkerAsync());
        await factory.DrainNotificationOutboxAsync();
        var ready = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Ready", ready.NextAction); Assert.Equal(failed.Progress.OrganizationId, ready.Progress!.OrganizationId);
        Assert.Equal(failed.Progress.SubscriptionId, ready.Progress.SubscriptionId); Assert.Equal(requestId, ready.Progress.ProvisioningRequestId);
        await WithDb(async db => {
            Assert.Single(await db.PlatformProvisioningRequests.Where(p => p.SchoolApplicationId == app.Id).ToArrayAsync());
            Assert.Single(await db.Subscriptions.Where(s => s.SourceOrderId == order.Order.OrderId).ToArrayAsync());
            Assert.Single(await db.Invitations.Where(i => i.OrganizationId == ready.Progress.OrganizationId && i.Role == OrganizationRole.Manager).ToArrayAsync());
            Assert.Single(await db.Payments.Where(p => p.OrderId == order.Order.OrderId).ToArrayAsync());
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.TargetId == requestId.ToString() && a.Action == "platform.provisioning.needs-attention"));
        });
    }

    [Fact]
    public async Task SchoolQuoteRevalidatesSelectedManagerAndUnpaidRepairCannotCreateProvisioning()
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var manager = await ActorAsync(); await CompleteSchoolManagerAsync(manager);
        var app = await SchoolDraftAsync(applicant); app = await SchoolAction(applicant, app, "submit"); app = await SchoolAction(admin, app, "review", "بررسی آزمون");
        app = await SchoolAction(admin, app, "decisions", "مدیر منتخب آزمون", SchoolApplicationStatus.Approved, manager.UserId);
        await WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == manager.UserId)).Suspend(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        using var quote = await applicant.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/quote", new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version), Json);
        Assert.Equal(HttpStatusCode.BadRequest, quote.StatusCode); Assert.Equal(SchoolApplicationErrorCodes.InitialManagerInvalid, await ProblemAsync(quote));
        var other = await ActorAsync();
        using var wrong = await other.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/provisioning/retry", new SchoolProvisioningRetryCommand(Guid.NewGuid(), app.Version, "غیرمجاز"), Json);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        using var unpaid = await admin.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/provisioning/retry", new SchoolProvisioningRetryCommand(Guid.NewGuid(), app.Version, "بدون پرداخت"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, unpaid.StatusCode);
        await WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == manager.UserId)).Reactivate(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        using var stillUnpaid = await admin.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/provisioning/retry", new SchoolProvisioningRetryCommand(Guid.NewGuid(), app.Version, "بازیابی فاقد پرداخت"), Json);
        Assert.Equal(HttpStatusCode.Conflict, stillUnpaid.StatusCode);
        await WithDb(async db => Assert.False(await db.PlatformProvisioningRequests.AnyAsync(p => p.SchoolApplicationId == app.Id)));
    }

    private async Task<CreateOrderResult> PaidSchoolOrderAsync(TestActor applicant, TestActor admin, SchoolApplicationResult app)
    {
        var version = await PlanAsync(SubscriptionOwnerType.Organization, priceIrr: 27000);
        var current = await WithDb(db => db.SchoolEstablishmentOfferings.Where(o => o.Kind == app.Kind).Select(o => (long?)o.Version).SingleOrDefaultAsync()) ?? 0;
        using var configured = await admin.Client.PutAsJsonAsync($"/api/admin/v1/school-applications/offerings/{app.Kind}", new SchoolOfferingCommand(Guid.NewGuid(), current, version, "فقط دادهٔ ساختگی تست"), Json);
        configured.EnsureSuccessStatusCode();
        var order = await PostAsync<CreateOrderResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}/orders", new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version));
        var payment = await PayAsync(applicant, order.Order.OrderId); await VerifyAsync(payment.PaymentAttemptId); return order;
    }

    [Fact]
    public async Task SchoolWorkerRechecksManagerAfterPaymentAndRecoveryDoesNotRequireAnotherPayment()
    {
        var admin = await AdminAsync(); var applicant = await ActorAsync(); var manager = await ActorAsync(); await CompleteSchoolManagerAsync(manager);
        var app = await SchoolDraftAsync(applicant); app = await SchoolAction(applicant, app, "submit"); app = await SchoolAction(admin, app, "review", "بررسی آزمون");
        app = await SchoolAction(admin, app, "decisions", "انتخاب مدیر آزمون", SchoolApplicationStatus.Approved, manager.UserId);
        var order = await PaidSchoolOrderAsync(applicant, admin, app);
        await WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == manager.UserId)).Suspend(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        await factory.DrainNotificationOutboxAsync();
        var failed = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Paid", failed.Progress!.PaymentState); Assert.Equal("NeedsAttention", failed.Progress.ProvisioningState); Assert.Null(failed.Progress.OrganizationId);
        Assert.Equal("ContactSupportForProvisioning", failed.NextAction);
        await WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == manager.UserId)).Reactivate(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        _ = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/provisioning/retry",
            new SchoolProvisioningRetryCommand(Guid.NewGuid(), failed.Version, "بازیابی پس از رفع تعلیق مدیر"));
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var ready = await GetAsync<SchoolApplicationResult>(applicant.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Ready", ready.NextAction); Assert.Equal(failed.Progress.ProvisioningRequestId, ready.Progress!.ProvisioningRequestId);
        await WithDb(async db => Assert.Single(await db.Payments.Where(p => p.OrderId == order.Order.OrderId).ToArrayAsync()));
    }
    private async Task<Guid> PrepareSchoolIntentAsync(Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = await scope.ServiceProvider.GetRequiredService<ISchoolProvisioningWorkHandler>().PreparePaidOrderAsync(orderId, default);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return id;
    }
    private async Task DispatchSchoolWorkerAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().DispatchBatchAsync(default);
    }
    private sealed class FailSchoolInvitationOnce : SaveChangesInterceptor
    {
        private bool _failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_failed && eventData.Context!.ChangeTracker.Entries<Invitation>().Any(e => e.State == EntityState.Added))
            { _failed = true; throw new DbUpdateException("شکست ساختگی و یک‌بارهٔ دعوت مدیر در تست ایزوله"); }
            return ValueTask.FromResult(result);
        }
    }
}
