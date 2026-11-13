using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    private async Task<SchoolApplicationResult> SchoolDraftAsync(TestActor actor, SchoolApplicationKind kind = SchoolApplicationKind.LegalOrganization)
    {
        var file = await CaseFileAsync(actor);
        return await PostAsync<SchoolApplicationResult>(actor.Client, "/api/v1/school-applications", new CreateSchoolApplicationCommand(Guid.NewGuid(), kind,
            new("مدرسه آزمون", "مؤسسه آزمون", "سابقه آزمون", "نمونه آزمون", null, 0, 0), [new(file.Id, kind == SchoolApplicationKind.LegalOrganization ? "License" : "Degree")]));
    }
    private Task<SchoolApplicationResult> SchoolAction(TestActor actor, SchoolApplicationResult app, string action, string? reason = null, SchoolApplicationStatus? decision = null, Guid? managerId = null) =>
        PostAsync<SchoolApplicationResult>(actor.Client, $"/api/{(action is "review" or "decisions" ? "admin/" : "")}v1/school-applications/{app.Id}/{action}",
            new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, reason, decision, managerId));
    private static async Task CompleteSchoolManagerAsync(TestActor actor)
    {
        using var response = await actor.Client.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("School", "Manager", null, "school." + Guid.NewGuid().ToString("N")[..20]), Json);
        response.EnsureSuccessStatusCode();
    }
    private async Task<SchoolApplicationResult> ApprovedSchoolAsync(TestActor owner, TestActor admin, SchoolApplicationKind kind = SchoolApplicationKind.LegalOrganization)
    {
        await CompleteSchoolManagerAsync(owner); var app = await SchoolDraftAsync(owner, kind);
        app = await SchoolAction(owner, app, "submit"); app = await SchoolAction(admin, app, "review", "شروع بررسی آزمون");
        return await SchoolAction(admin, app, "decisions", "تأیید آزمون", SchoolApplicationStatus.Approved, owner.UserId);
    }

    [Theory]
    [InlineData(SchoolApplicationKind.LegalOrganization)] [InlineData(SchoolApplicationKind.IndividualTeacher)]
    public async Task SchoolApplicationSubmitReviewRevisionAndHistoryArePrivateVersionedAndReplaySafe(SchoolApplicationKind kind)
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var stranger = await ActorAsync();
        var app = await SchoolDraftAsync(owner, kind); var file = Assert.Single(app.Documents).FileAssetId;
        Assert.True(app.CanSubmit); Assert.True(app.CanEdit); Assert.Empty(app.MissingRequirements);
        var command = new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version);
        app = await PostAsync<SchoolApplicationResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/submit", command);
        var replay = await PostAsync<SchoolApplicationResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/submit", command);
        Assert.Equal(app.Version, replay.Version); Assert.Equal(SchoolApplicationStatus.Submitted, app.Status);
        var mine = await GetAsync<SchoolApplicationPage>(owner.Client, "/api/v1/school-applications"); Assert.Contains(mine.Items, a => a.Id == app.Id);
        var theirs = await GetAsync<SchoolApplicationPage>(stranger.Client, "/api/v1/school-applications"); Assert.DoesNotContain(theirs.Items, a => a.Id == app.Id);
        var queue = await GetAsync<SchoolApplicationPage>(admin.Client, "/api/admin/v1/school-applications?status=Submitted"); Assert.Contains(queue.Items, a => a.Id == app.Id);
        using var ownerDoc = await owner.Client.GetAsync($"/api/v1/files/{file}/content"); Assert.Equal(HttpStatusCode.OK, ownerDoc.StatusCode);
        using var reviewerDoc = await admin.Client.GetAsync($"/api/v1/files/{file}/content"); Assert.Equal(HttpStatusCode.OK, reviewerDoc.StatusCode);
        using var strangerDoc = await stranger.Client.GetAsync($"/api/v1/files/{file}/content"); Assert.Equal(HttpStatusCode.NotFound, strangerDoc.StatusCode);
        using var strangerHistory = await stranger.Client.GetAsync($"/api/v1/school-applications/{app.Id}/history"); Assert.Equal(HttpStatusCode.NotFound, strangerHistory.StatusCode);
        using var noReason = await admin.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/review", new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version), Json);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        app = await SchoolAction(admin, app, "review", "بررسی مدارک");
        app = await SchoolAction(admin, app, "decisions", "نیاز به اصلاح نام", SchoolApplicationStatus.NeedsChanges);
        using var update = await owner.Client.PutAsJsonAsync($"/api/v1/school-applications/{app.Id}", new UpdateSchoolApplicationCommand(Guid.NewGuid(), app.Version,
            app.Data with { SchoolName = "نام اصلاح‌شده" }), Json); update.EnsureSuccessStatusCode();
        app = (await update.Content.ReadFromJsonAsync<SchoolApplicationResult>(Json))!;
        Assert.Equal(2, app.Revision); Assert.Equal(file, Assert.Single(app.Documents).FileAssetId);
        app = await SchoolAction(owner, app, "submit"); app = await SchoolAction(admin, app, "review", "بررسی نسخه جدید");
        await CompleteSchoolManagerAsync(owner);
        var decision = new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, "تأیید پس از اصلاح", SchoolApplicationStatus.Approved, owner.UserId);
        app = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/decisions", decision);
        var duplicate = await PostAsync<SchoolApplicationResult>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/decisions", decision);
        Assert.Equal(app.Version, duplicate.Version); Assert.False(app.CanEdit); Assert.False(app.CanCancel);
        var history = await GetAsync<SchoolApplicationHistoryPage>(owner.Client, $"/api/v1/school-applications/{app.Id}/history");
        Assert.Equal(8, history.Total); Assert.Equal(2, history.Items.Count(h => h.Action == "school_application.Submitted"));
        Assert.Single(history.Items, h => h.Action == "school_application.Approved"); Assert.All(history.Items, h => Assert.Null(h.ActorUserId));
        var audit = await GetAsync<SchoolApplicationHistoryPage>(admin.Client, $"/api/admin/v1/school-applications/{app.Id}/history");
        Assert.All(audit.Items, h => Assert.NotNull(h.ActorUserId));
        using var locked = await owner.Client.PutAsJsonAsync($"/api/v1/school-applications/{app.Id}", new UpdateSchoolApplicationCommand(Guid.NewGuid(), app.Version, app.Data), Json);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        await WithDb(async db => { Assert.False(await db.Memberships.AnyAsync(m => m.UserId == owner.UserId)); Assert.False(await db.Orders.AnyAsync(o => o.SchoolApplicationId == app.Id)); });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SchoolApplicationCancelIsFinalAndIdempotentWithoutDeletingRevision(bool submitted)
    {
        var owner = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        if (submitted) app = await SchoolAction(owner, app, "submit");
        var command = new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, "انصراف آزمون");
        var cancelled = await PostAsync<SchoolApplicationResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/cancel", command);
        var repeated = await PostAsync<SchoolApplicationResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/cancel", command);
        Assert.Equal(SchoolApplicationStatus.Cancelled, cancelled.Status); Assert.Equal(cancelled.Version, repeated.Version);
        using var submit = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/submit", command with { ClientOperationId = Guid.NewGuid(), ExpectedVersion = cancelled.Version }, Json);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        var history = await GetAsync<SchoolApplicationHistoryPage>(owner.Client, $"/api/v1/school-applications/{app.Id}/history");
        Assert.Single(history.Items, h => h.Action == "school_application.Cancelled");
        await WithDb(async db => Assert.Equal(1, await db.SchoolApplicationRevisions.CountAsync(r => r.ApplicationId == app.Id)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SchoolApplicationConcurrentDecisionsHaveOneWinnerAndNoDuplicateHistory(bool crossReviewerTargets)
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        var otherReviewer = await ActorAsync();
        using var grant = await admin.Client.PostAsJsonAsync("/api/admin/v1/role-assignments",
            new GrantPlatformRoleCommand(otherReviewer.UserId, PlatformRoleKeys.PlatformOwner, "دومین بازبین مستقل آزمون رقابت"), Json);
        grant.EnsureSuccessStatusCode();
        app = await SchoolAction(owner, app, "submit"); app = await SchoolAction(admin, app, "review", "شروع بررسی");
        await CompleteSchoolManagerAsync(owner); await CompleteSchoolManagerAsync(otherReviewer); await CompleteSchoolManagerAsync(admin);
        var actions = new[] { (Actor: admin, Manager: crossReviewerTargets ? otherReviewer.UserId : owner.UserId),
            (Actor: otherReviewer, Manager: crossReviewerTargets ? admin.UserId : otherReviewer.UserId) };
        var responses = await Task.WhenAll(actions.Select(a => a.Actor.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/decisions",
            new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, "تصمیم هم‌زمان آزمون", SchoolApplicationStatus.Approved, a.Manager), Json)));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        await WithDb(async db => { Assert.Equal(2, await db.SchoolApplicationDecisions.CountAsync(d => d.ApplicationId == app.Id)); Assert.Equal(4, await db.SchoolApplicationHistories.CountAsync(h => h.ApplicationId == app.Id)); });
    }

    [Fact]
    public async Task SchoolApplicationCrossUserAndOrganizationManagerHaveNoReviewOrMutationPermission()
    {
        var owner = await ActorAsync(); var other = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        var org = await factory.CreateOrganizationAsync("سازمان آزمون مجوز"); await factory.CreateMembershipAsync(other.UserId, org, OrganizationRole.Manager);
        using var edit = await other.Client.PutAsJsonAsync($"/api/v1/school-applications/{app.Id}", new UpdateSchoolApplicationCommand(Guid.NewGuid(), app.Version, app.Data), Json);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        using var review = await other.Client.PostAsJsonAsync($"/api/admin/v1/school-applications/{app.Id}/review", new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version, "غیرمجاز"), Json);
        Assert.Equal(HttpStatusCode.Forbidden, review.StatusCode);
        using var cancel = await other.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/cancel", new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version), Json);
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SchoolApplicationCannotBindAnotherOwnersOrDeletedDocument(bool deleted)
    {
        var owner = await ActorAsync(); var other = await ActorAsync(); var file = await CaseFileAsync(deleted ? owner : other);
        if (deleted) await WithDb(async db => { (await db.FileAssets.SingleAsync(f => f.Id == file.Id)).MarkDeleted(factory.Clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); });
        using var response = await owner.Client.PostAsJsonAsync("/api/v1/school-applications", new CreateSchoolApplicationCommand(Guid.NewGuid(), SchoolApplicationKind.LegalOrganization,
            new("مدرسه", "مؤسسه", null, null, null, 0, 0), [new(file.Id, "License")]), Json);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal(SchoolApplicationErrorCodes.FileUnavailable, await ProblemAsync(response));
    }

    [Fact]
    public async Task SchoolApplicationDocumentRemainsBoundAndReviewerRevocationIsCurrent()
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var app = await SchoolDraftAsync(owner); var file = Assert.Single(app.Documents).FileAssetId;
        app = await SchoolAction(owner, app, "submit");
        var metadata = await GetAsync<MakanApp.Application.Storage.FileAssetResult>(owner.Client, $"/api/v1/files/{file}");
        using var boundDelete = await owner.Client.DeleteAsync($"/api/v1/files/{file}?expectedRowVersion={Uri.EscapeDataString(metadata.RowVersion)}");
        Assert.Equal(HttpStatusCode.Conflict, boundDelete.StatusCode); Assert.Equal(MakanApp.Application.Storage.StorageErrorCodes.FileInUse, await ProblemAsync(boundDelete));
        using var allowed = await admin.Client.GetAsync($"/api/v1/files/{file}"); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await factory.ResetPlatformIamAsync();
        using var revoked = await admin.Client.GetAsync($"/api/v1/files/{file}"); Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }

    [Theory]
    [InlineData("Draft")] [InlineData("Submitted")] [InlineData("Rejected")] [InlineData("Cancelled")]
    public async Task SchoolApplicationCannotQuoteBeforeApproval(string status)
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        if (status is "Submitted" or "Rejected") app = await SchoolAction(owner, app, "submit");
        if (status == "Rejected") { app = await SchoolAction(admin, app, "review", "شروع"); app = await SchoolAction(admin, app, "decisions", "رد آزمون", SchoolApplicationStatus.Rejected); }
        if (status == "Cancelled") app = await SchoolAction(owner, app, "cancel");
        using var quote = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/quote", new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version), Json);
        Assert.Equal(HttpStatusCode.Conflict, quote.StatusCode); Assert.Equal(SchoolApplicationErrorCodes.NotApproved, await ProblemAsync(quote));
    }

    [Fact]
    public async Task SchoolApplicationRequiresConfiguredOrganizationOfferingAndCannotAcceptArbitraryClientPlan()
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var app = await ApprovedSchoolAsync(owner, admin);
        await WithDb(db => db.SchoolEstablishmentOfferings.ExecuteDeleteAsync());
        using var missing = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/quote", new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version), Json);
        Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode); Assert.Equal(SchoolApplicationErrorCodes.CommercialTermsUnavailable, await ProblemAsync(missing));
        var personalPlan = await PlanAsync(SubscriptionOwnerType.Individual);
        using var badMapping = await admin.Client.PutAsJsonAsync("/api/admin/v1/school-applications/offerings/LegalOrganization", new SchoolOfferingCommand(Guid.NewGuid(), 0, personalPlan, "آزمون پلن ناسازگار"), Json);
        Assert.Equal(HttpStatusCode.Conflict, badMapping.StatusCode);
        using var arbitraryPlan = await owner.Client.PostAsJsonAsync("/api/v1/commerce/personal/orders",
            new CreateOrderCommand(personalPlan, Guid.NewGuid()) { SchoolApplicationId = app.Id, SchoolApplicationVersion = app.Version }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, arbitraryPlan.StatusCode);
    }

    [Theory]
    [InlineData(SchoolApplicationKind.LegalOrganization)] [InlineData(SchoolApplicationKind.IndividualTeacher)]
    public async Task SchoolApplicationServerTermsOrderVerifiedPaymentAndDurableProvisioningBoundaryAreReplaySafe(SchoolApplicationKind kind)
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var other = await ActorAsync();
        var app = await ApprovedSchoolAsync(owner, admin, kind);
        await WithDb(db => db.SchoolEstablishmentOfferings.ExecuteDeleteAsync());
        var version = await PlanAsync(SubscriptionOwnerType.Organization, priceIrr: 23000);
        var mapping = new SchoolOfferingCommand(Guid.NewGuid(), 0, version, "نگاشت خدمت ساختگی تست");
        using var configured = await admin.Client.PutAsJsonAsync($"/api/admin/v1/school-applications/offerings/{kind}", mapping, Json); configured.EnsureSuccessStatusCode();
        using var mappingReplay = await admin.Client.PutAsJsonAsync($"/api/admin/v1/school-applications/offerings/{kind}", mapping, Json); mappingReplay.EnsureSuccessStatusCode();
        var quote = await PostAsync<CommercialQuoteDto>(owner.Client, $"/api/v1/school-applications/{app.Id}/quote", new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version));
        Assert.Equal(version, quote.PlanVersionId); Assert.Equal(app.Id, quote.SchoolApplicationId); Assert.Equal(2300, quote.PriceToman);
        var command = new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version);
        var created = await PostAsync<CreateOrderResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/orders", command);
        Assert.Equal(app.Id, created.Order.SchoolApplicationId); Assert.Equal(23000, created.Order.Amount);
        var nextVersion = await PlanAsync(SubscriptionOwnerType.Organization, priceIrr: 44000);
        using var changedMapping = await admin.Client.PutAsJsonAsync($"/api/admin/v1/school-applications/offerings/{kind}",
            new SchoolOfferingCommand(Guid.NewGuid(), 1, nextVersion, "تغییر نگاشت فقط برای خرید آینده"), Json); changedMapping.EnsureSuccessStatusCode();
        var catalog = await GetAsync<IReadOnlyList<SchoolOfferingResult>>(admin.Client, "/api/admin/v1/school-applications/offerings");
        Assert.Equal(2, Assert.Single(catalog).Version);
        var org = await factory.CreateOrganizationAsync("سازمان آزمون جداسازی محصول");
        var membership = await factory.CreateMembershipAsync(owner.UserId, org, OrganizationRole.Manager);
        await SelectAsync(owner, membership.MembershipId, OrganizationRole.Manager);
        foreach (var offeringVersion in new[] { version, nextVersion })
        {
            using var ordinaryPurchase = await owner.Client.PostAsJsonAsync("/api/v1/commerce/orders", new CreateOrderCommand(offeringVersion, Guid.NewGuid()), Json);
            Assert.Equal(HttpStatusCode.Conflict, ordinaryPurchase.StatusCode);
            Assert.Equal(CommerceErrorCodes.PlanNotPurchasable, await ProblemAsync(ordinaryPurchase));
        }
        using var historicalMappingReplay = await admin.Client.PutAsJsonAsync($"/api/admin/v1/school-applications/offerings/{kind}", mapping, Json);
        historicalMappingReplay.EnsureSuccessStatusCode();
        Assert.Equal(version, (await historicalMappingReplay.Content.ReadFromJsonAsync<SchoolOfferingResult>(Json))!.PlanVersionId);
        var replay = await PostAsync<CreateOrderResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/orders", command);
        Assert.True(replay.Replayed); Assert.Equal(created.Order.OrderId, replay.Order.OrderId); Assert.Equal(23000, replay.Order.Amount); Assert.Equal(version, replay.Order.PlanVersionId);
        using var duplicateIntent = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/orders", command with { ClientOperationId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicateIntent.StatusCode);
        using var foreign = await other.Client.GetAsync($"/api/v1/commerce/personal/orders/{created.Order.OrderId}"); Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        var attempt = await PayAsync(owner, created.Order.OrderId); await VerifyAsync(attempt.PaymentAttemptId); await VerifyAsync(attempt.PaymentAttemptId);
        var pending = await GetAsync<SchoolApplicationResult>(owner.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Paid", pending.Progress!.PaymentState); Assert.Equal("PaidAwaitingProvisioning", pending.Progress.ProvisioningState);
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        var paid = await GetAsync<SchoolApplicationResult>(owner.Client, $"/api/v1/school-applications/{app.Id}");
        Assert.Equal("Completed", paid.Progress!.ProvisioningState); Assert.NotNull(paid.Progress.ProvisioningRequestId);
        Assert.NotNull(paid.Progress.OrganizationId); Assert.NotNull(paid.Progress.SubscriptionId); Assert.NotNull(paid.Progress.ManagerInvitationId);
        Assert.Equal("Ready", paid.NextAction); Assert.Equal(owner.UserId, paid.FinalInitialManagerUserId);
        Assert.Equal("IRR", paid.Progress.Currency); Assert.Equal(23000, paid.Progress.Amount); Assert.Equal(version, paid.Progress.PlanVersionId);
        var commercial = await GetAsync<CommercialSummaryDto>(admin.Client, $"/api/admin/v1/commerce/organizations/{paid.Progress.OrganizationId}/subscription");
        Assert.True(commercial.HasVerifiedCommercialContract); Assert.True(commercial.CommercialControlEnabled);
        Assert.True(commercial.Entitlements[EntitlementKey.Academic]); Assert.Equal(version, commercial.PlanVersionId);
        Assert.Equal(5, commercial.Quotas.Single(q => q.Key == QuotaKey.ActiveLearners).Limit);
        await WithDb(async db => {
            Assert.Equal(1, await db.Orders.CountAsync(o => o.SchoolApplicationId == app.Id));
            Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.Action == "school_offering.Configured" && a.ActorUserId == admin.UserId && a.Reason == mapping.Reason));
            Assert.Equal(1, await db.Payments.CountAsync(p => p.OrderId == created.Order.OrderId));
            Assert.Equal(1, await db.PlatformProvisioningRequests.CountAsync(p => p.SchoolApplicationId == app.Id && p.SourceOrderId == created.Order.OrderId));
            var subscription = Assert.Single(await db.Subscriptions.Where(s => s.SourceOrderId == created.Order.OrderId).ToArrayAsync());
            Assert.True(subscription.CommercialControlEnabled); Assert.NotNull(subscription.EndsAtUtc); Assert.Equal(version, subscription.PlanVersionId);
            Assert.Equal(paid.Progress.OrganizationId, subscription.OrganizationId);
            Assert.Equal(1, await db.Memberships.CountAsync(m => m.UserId == owner.UserId));
        });
        using var accept = await owner.Client.PostAsync($"/api/v1/invitations/{paid.Progress.ManagerInvitationId}/accept", null); accept.EnsureSuccessStatusCode();
        using var acceptRetry = await owner.Client.PostAsync($"/api/v1/invitations/{paid.Progress.ManagerInvitationId}/accept", null); acceptRetry.EnsureSuccessStatusCode();
        await WithDb(async db => Assert.Equal(1, await db.Memberships.CountAsync(m => m.UserId == owner.UserId && m.OrganizationId == paid.Progress.OrganizationId)));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SchoolApplicationMutationRejectsSuspendedAccountOrRevokedSession(bool suspended)
    {
        var owner = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        await WithDb(async db => {
            if (suspended) (await db.Users.SingleAsync(u => u.Id == owner.UserId)).Suspend(factory.Clock.GetUtcNow().UtcDateTime);
            else (await db.UserSessions.SingleAsync(s => s.Id == owner.SessionId)).Revoke(factory.Clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
        });
        using var response = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/submit", new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version), Json);
        // احراز نشست پیش از Use Case، حساب معلق و نشست لغوشده را هر دو نامعتبر می‌داند.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await WithDb(async db => Assert.Equal(SchoolApplicationStatus.Draft, (await db.SchoolApplications.SingleAsync(a => a.Id == app.Id)).Status));
    }

    [Fact]
    public async Task SchoolApplicationCancelBeforeSubmissionKeepsDraftDocumentsPrivateFromReviewer()
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        var file = Assert.Single(app.Documents).FileAssetId; await SchoolAction(owner, app, "cancel");
        using var hidden = await admin.Client.GetAsync($"/api/admin/v1/school-applications/{app.Id}"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var document = await admin.Client.GetAsync($"/api/v1/files/{file}"); Assert.Equal(HttpStatusCode.NotFound, document.StatusCode);
    }

    [Fact]
    public async Task SchoolApplicationSubmitOperationAndVersionCannotBeReusedWithChangedPayload()
    {
        var owner = await ActorAsync(); var app = await SchoolDraftAsync(owner);
        var command = new SchoolApplicationActionCommand(Guid.NewGuid(), app.Version);
        using var oversized = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/submit", command with { ApplicantFeedback = new string('x', 2001) }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode); Assert.Equal(SchoolApplicationErrorCodes.Invalid, await ProblemAsync(oversized));
        var submitted = await PostAsync<SchoolApplicationResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/submit", command);
        using var conflict = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/submit", command with { ApplicantFeedback = "مقدار متفاوت" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var stale = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/cancel", command with { ClientOperationId = Guid.NewGuid() }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await WithDb(async db => Assert.Equal(submitted.Version, (await db.SchoolApplications.SingleAsync(a => a.Id == app.Id)).Version));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SchoolApplicationExpiredOrderCanOnlyBeReplacedWhenNoPaymentWasAttempted(bool attempted)
    {
        var admin = await AdminAsync(); var owner = await ActorAsync(); var app = await ApprovedSchoolAsync(owner, admin);
        var version = await PlanAsync(SubscriptionOwnerType.Organization);
        var mappings = await GetAsync<IReadOnlyList<SchoolOfferingResult>>(admin.Client, "/api/admin/v1/school-applications/offerings");
        using var configure = await admin.Client.PutAsJsonAsync("/api/admin/v1/school-applications/offerings/LegalOrganization",
            new SchoolOfferingCommand(Guid.NewGuid(), mappings.SingleOrDefault(o => o.Kind == app.Kind)?.Version ?? 0, version, "نگاشت آزمون انقضای سفارش"), Json);
        configure.EnsureSuccessStatusCode();
        var command = new SchoolApplicationCommerceCommand(Guid.NewGuid(), app.Version);
        var first = await PostAsync<CreateOrderResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/orders", command);
        if (attempted) await PayAsync(owner, first.Order.OrderId);
        factory.Clock.SetUtcNow(factory.Clock.GetUtcNow().UtcDateTime.AddMinutes(16));
        using var retry = await owner.Client.PostAsJsonAsync($"/api/v1/school-applications/{app.Id}/orders", command with { ClientOperationId = Guid.NewGuid() }, Json);
        if (attempted) Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        else
        {
            retry.EnsureSuccessStatusCode(); var next = (await retry.Content.ReadFromJsonAsync<CreateOrderResult>(Json))!;
            Assert.NotEqual(first.Order.OrderId, next.Order.OrderId); Assert.Equal(first.Order.Amount, next.Order.Amount);
            var replay = await PostAsync<CreateOrderResult>(owner.Client, $"/api/v1/school-applications/{app.Id}/orders", command);
            Assert.True(replay.Replayed); Assert.Equal(first.Order.OrderId, replay.Order.OrderId); Assert.Equal(OrderStatus.Cancelled, replay.Order.Status);
        }
    }
}
