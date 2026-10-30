using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Ai;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AiChatEndpointsTests
{
    [Fact]
    public async Task RestrictedCorsAllowsAiInvocationHeadersOnlyForApprovedOriginsAndExposesOperationId()
    {
        const string origin = "https://ai-client.example.test";
        using var application = factory.WithWebHostBuilder(_ => { });
        using var client = CreateRestrictedClient();
        foreach (var selectedOrigin in new[] { origin, "https://unapproved.example.test" })
        {
            using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/ai/students/analyze");
            preflight.Headers.Add("Origin", selectedOrigin); preflight.Headers.Add("Access-Control-Request-Method", "POST");
            preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,x-ai-operation-id,x-ai-payer");
            using var response = await client.SendAsync(preflight);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            if (selectedOrigin == origin)
            {
                Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
                var allowed = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
                Assert.Contains("x-ai-operation-id", allowed, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("x-ai-payer", allowed, StringComparison.OrdinalIgnoreCase);
            }
            else Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
        using var health = new HttpRequestMessage(HttpMethod.Get, "/health/live"); health.Headers.Add("Origin", origin);
        using var healthy = await client.SendAsync(health); healthy.EnsureSuccessStatusCode();
        Assert.Contains("X-AI-Operation-Id", string.Join(",", healthy.Headers.GetValues("Access-Control-Expose-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.AiService.ProviderCallCount);

        HttpClient CreateRestrictedClient()
        {
            // Program پیش از callback دیرهنگام TestHost مقدار CORS را می‌خواند؛ فقط هنگام ساخت این میزبان override می‌شود.
            var previousMode = Environment.GetEnvironmentVariable("Cors__AllowAnyOrigin");
            var previousOrigin = Environment.GetEnvironmentVariable("Cors__AllowedOrigins__0");
            try
            {
                Environment.SetEnvironmentVariable("Cors__AllowAnyOrigin", "false");
                Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", origin);
                return application.CreateClient();
            }
            finally
            {
                Environment.SetEnvironmentVariable("Cors__AllowAnyOrigin", previousMode);
                Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", previousOrigin);
            }
        }
    }

    [Fact]
    public async Task AiInvocationOpenApiListsOptionalHeadersWithoutInvokingAi()
    {
        using var client = factory.CreateClient(); using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var parameters = json.RootElement.GetProperty("paths").GetProperty("/api/v1/ai/chat").GetProperty("post").GetProperty("parameters");
        Assert.Contains(parameters.EnumerateArray(), p => p.GetProperty("name").GetString() == "X-AI-Operation-Id");
        Assert.Contains(parameters.EnumerateArray(), p => p.GetProperty("name").GetString() == "X-AI-Payer");
        Assert.Equal(0, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task RegisteredAiHttpClientNeverRedirectsPrivatePayloadToAnotherOrigin()
    {
        await using var origin = await ControlledAiV1Server.StartAsync();
        await using var destination = await ControlledAiV1Server.StartAsync();
        origin.RedirectLocation = destination.BaseUrl + "/api/v1/ai/chat";
        using var http = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IAiServiceClient));
        var client = AiV1ControlledTransportTests.Client(origin, http);
        var error = await Assert.ThrowsAsync<AiServiceException>(() => client.ChatAsync((AiChatRequest)AiV1ControlledTransportTests.Requests()[0], default));
        Assert.Equal(AiServiceErrorCodes.ServiceUnavailable, error.Code); Assert.Equal(302, error.UpstreamStatusCode);
        Assert.Equal(1, origin.BusinessCalls); Assert.Empty(destination.Events); Assert.Equal(0, destination.BusinessCalls);
    }
    private async Task EnsureAiContractAsync(Guid org, long limit = 1000, bool grantAi = true, Guid? account = null)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var owner = account.HasValue ? SubscriptionOwner.Individual(account.Value) : SubscriptionOwner.Organization(org);
        if (await db.Subscriptions.AnyAsync(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id && s.CommercialControlEnabled)) return;
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var plan = Plan.Create("ai-isolated." + Guid.NewGuid().ToString("N"), "قرارداد ساختگی تست AI", now.AddDays(-2), owner.Type);
        var version = PlanVersion.CreateDraft(plan.Id, 1, now.AddDays(-2)); version.SetMonthlyTerms(BillingCalendar.Gregorian, "synthetic-not-for-sale", 80, 95);
        version.SetPrice(1000, CurrencyCode.Irr);
        foreach (var feature in new[] { EntitlementKey.Academic, EntitlementKey.Assignments, EntitlementKey.Exams }) version.SetEntitlement(feature, true);
        version.SetEntitlement(EntitlementKey.Ai, grantAi);
        version.SetQuota(QuotaKey.AiTextRequests, limit); version.SetQuota(QuotaKey.ActiveClasses, 100); version.SetQuota(QuotaKey.ActiveLearners, 100);
        version.Publish(now.AddDays(-1)); db.Plans.Add(plan); db.PlanVersions.Add(version);
        db.Subscriptions.Add(Subscription.CreateMonthly(owner, version, now.AddHours(-1), 0, now, null, true)); await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AiInvocationConsumesOnceAndCompletedRetryNeverCallsProviderAgain()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI metered")); var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken); await EnsureAiContractAsync(org, 1); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        var id = Guid.NewGuid(); var command = new SendAiChatCommand("پرسش ساختگی", null) { OperationId = id };
        using (var first = await client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions)) first.EnsureSuccessStatusCode();
        using (var replay = await client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions))
        { Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal(AiInvocationErrorCodes.Completed, await ReadProblemCodeAsync(replay)); }
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        using (var changed = await client.PostAsJsonAsync("/api/v1/ai/chat", command with { Message = "قصد تازه" }, JsonOptions)) Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using (var exhausted = await client.PostAsJsonAsync("/api/v1/ai/chat", command with { OperationId = Guid.NewGuid() }, JsonOptions)) Assert.Equal(HttpStatusCode.Conflict, exhausted.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var reservation = await db.UsageReservations.SingleAsync(x => x.ConsumerUserId == user.User.Id && x.OperationId == id);
        Assert.Equal(UsageReservationStatus.Consumed, reservation.Status);
        Assert.Equal(1, await db.UsageLedgerEntries.CountAsync(x => x.SourceOperationId == id && x.Status == UsageReservationStatus.Consumed));
    }

    [Theory]
    [InlineData(AiServiceErrorCodes.ServiceUnauthorized, UsageReservationStatus.Released)]
    [InlineData(AiServiceErrorCodes.RateLimited, UsageReservationStatus.Released)]
    [InlineData(AiServiceErrorCodes.ServiceTimeout, UsageReservationStatus.Unknown)]
    [InlineData(AiServiceErrorCodes.ResponseInvalid, UsageReservationStatus.Unknown)]
    public async Task AiInvocationFailureNeverWritesSuccessfulUsageOrRetriesProvider(string code, UsageReservationStatus status)
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI outcome")); var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        factory.AiService.FailNext(new AiServiceException(code, "خطای کنترل‌شده")); var id = Guid.NewGuid();
        var command = new SendAiChatCommand("پرسش ساختگی", null) { OperationId = id };
        using (var failure = await client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions)) Assert.False(failure.IsSuccessStatusCode);
        using (var replay = await client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions)) Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(status, (await db.UsageReservations.SingleAsync(x => x.ConsumerUserId == user.User.Id && x.OperationId == id)).Status);
        Assert.False(await db.UsageLedgerEntries.AnyAsync(x => x.SourceOperationId == id && x.Status == UsageReservationStatus.Consumed));
    }

    [Fact]
    public async Task DisabledAiNeverReserves()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI disabled")); var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        factory.AiService.Enabled = false; var id = Guid.NewGuid();
        using (var disabled = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null) { OperationId = id }, JsonOptions))
        { Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode); Assert.Equal(AiServiceErrorCodes.FeatureDisabled, await ReadProblemCodeAsync(disabled)); }
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.AnyAsync(x => x.OperationId == id));
    }

    [Fact]
    public async Task AiInvocationConcurrentRetriesConsumeOneUnitAndInvokeOnce()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI concurrent")); var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        var command = new SendAiChatCommand("پرسش ساختگی", null) { OperationId = Guid.NewGuid() };
        var requests = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions)));
        Assert.Single(requests, r => r.IsSuccessStatusCode); Assert.Equal(3, requests.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, factory.AiService.ProviderCallCount); foreach (var response in requests) response.Dispose();
    }

    [Fact]
    public async Task AiInvocationNoContractNeverFallsBackButExplicitTrialRemainsThreeSuccessfulAccountRequests()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI no contract")); var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        using (var selected = await client.PostAsJsonAsync("/api/v1/workspaces/select", new MakanApp.Application.Organization.SelectWorkspaceCommand(WorkspaceType.Organization, member.MembershipId, OrganizationRole.Manager), JsonOptions)) selected.EnsureSuccessStatusCode();
        using (var noContract = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null), JsonOptions))
        { Assert.Equal(HttpStatusCode.Conflict, noContract.StatusCode); Assert.Equal(0, factory.AiService.ProviderCallCount); }
        client.DefaultRequestHeaders.Add("X-AI-Payer", "FreeTrial");
        for (var i = 0; i < 3; i++)
        { using var trial = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null) { OperationId = Guid.NewGuid() }, JsonOptions); trial.EnsureSuccessStatusCode(); }
        using var exhausted = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null), JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, exhausted.StatusCode); Assert.Equal(3, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AiInvocationUnauthorizedRoleAndMalformedHeadersNeverReserve()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI boundary")); var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        using (var denied = await client.PostAsync("/api/v1/ai/students/analyze", null)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Add("X-AI-Operation-Id", "invalid-id");
        using (var invalid = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null), JsonOptions)) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.AnyAsync(x => x.ConsumerUserId == user.User.Id));
    }

    [Fact]
    public async Task AiInvocationPaidContractWithoutAiEntitlementNeverReservesOrInvokes()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI entitlement denied"));
        var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        await EnsureAiContractAsync(org, grantAi: false);
        UseBearerToken(client, user.AccessToken); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        using var denied = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null), JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); Assert.Equal(0, factory.AiService.ProviderCallCount);
        await using var scope = factory.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.AnyAsync(x => x.ConsumerUserId == user.User.Id));
    }

    [Fact]
    public async Task AiInvocationExplicitIndividualPayerKeepsAuthorizedOrganizationDataAndChargesOnlyAccount()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI individual payer"));
        var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        await EnsureAiContractAsync(org, account: user.User.Id); client.DefaultRequestHeaders.Add("X-AI-Payer", "Individual");
        var id = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش", null) { OperationId = id }, JsonOptions);
        response.EnsureSuccessStatusCode(); Assert.Equal(org.ToString("D"), factory.AiService.LastRequest!.Scope.OrganizationId);
        await using var scope = factory.Services.CreateAsyncScope(); var reservation = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().UsageReservations.SingleAsync(x => x.OperationId == id);
        Assert.Equal(SubscriptionOwnerType.Individual, reservation.OwnerType); Assert.Equal(user.User.Id, reservation.OwnerId);
        Assert.Equal(UsageReservationStatus.Consumed, reservation.Status); Assert.Equal(1, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AiInvocationMembershipRevokedAfterReservationReleasesWithoutCallingProvider()
    {
        using var client = factory.CreateClient(); var user = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI revoked during reserve"));
        var member = await factory.CreateMembershipAsync(user.User.Id, org, OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken); await SelectWorkspaceAsync(client, member.MembershipId, OrganizationRole.Manager);
        var context = (await client.GetFromJsonAsync<AccessContext>("/api/v1/workspaces/current", JsonOptions))!;
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var executor = scope.ServiceProvider.GetRequiredService<IAiInvocationExecutor>(); var operationId = Guid.NewGuid();
        var request = new AiChatRequest(Guid.NewGuid().ToString("N"), "revocation-check", null,
            new(user.User.Id.ToString("D"), "Manager"), new(org.ToString("D"), org.ToString("D"), null, null),
            new("پرسش ساختگی"), AiAuthorizedContext.Empty);
        var called = false;
        await Assert.ThrowsAnyAsync<Exception>(() => executor.ExecuteAsync<AiChatRequest, AiChatResponse>(user.User.Id, context.SessionId, request,
            (_, _) => { called = true; throw new InvalidOperationException("نباید فراخوانی شود."); }, async ct =>
            {
                Assert.True(await db.UsageReservations.AnyAsync(x => x.OperationId == operationId && x.Status == UsageReservationStatus.Reserved, ct));
                (await db.Memberships.SingleAsync(x => x.Id == member.MembershipId, ct)).End(factory.Clock.GetUtcNow().UtcDateTime);
                await db.SaveChangesAsync(ct);
            }, new(operationId), default));
        Assert.False(called); db.ChangeTracker.Clear();
        Assert.Equal(UsageReservationStatus.Released, (await db.UsageReservations.SingleAsync(x => x.OperationId == operationId)).Status);
        Assert.False(await db.UsageLedgerEntries.AnyAsync(x => x.SourceOperationId == operationId && x.Status == UsageReservationStatus.Consumed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AiAcademicScopeRequiresCurrentClassAndLearnerEvenWhenEnrollmentRemainsActive(bool endClass)
    {
        using var client = factory.CreateClient(); var teacher = await CreateAuthenticatedUserAsync(client);
        var org = await factory.CreateOrganizationAsync(NewName("AI current academic state"));
        var member = await factory.CreateMembershipAsync(teacher.User.Id, org, OrganizationRole.Teacher);
        var learner = await factory.CreateOrganizationPersonAsync(org, await factory.CreatePersonWithoutUserAsync());
        var academicClass = await factory.CreateAcademicClassAsync(org);
        var enrollment = await factory.CreateEnrollmentAsync(org, academicClass.ClassId, learner);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var assignment = Assignment.CreateDraft(org, academicClass.ClassId, member.MembershipId, now);
        var homework = AssignmentVersion.CreateDraft(org, academicClass.ClassId, assignment.Id, 1, "تکلیف تست", "توضیح ساختگی تست",
            now.AddDays(1), false, 1, 10m, member.MembershipId, now);
        homework.Publish(now); assignment.Publish(now); db.Assignments.Add(assignment); db.AssignmentVersions.Add(homework);
        db.AssignmentRecipients.Add(AssignmentRecipient.Create(org, academicClass.ClassId, assignment.Id, homework.Id, enrollment, now));
        var exam = Exam.CreateDraft(org, academicClass.ClassId, member.MembershipId, now);
        var examVersion = ExamVersion.CreateDraft(org, exam.Id, 1, "آزمون تست", null, now, now.AddDays(1), 30, 1, 10m,
            ExamRandomizationPolicy.None, member.MembershipId, now);
        var question = QuestionVersion.CreateDraft(org, examVersion.Id, 1, ExamQuestionType.Descriptive, "پرسش", 10m, null, now);
        examVersion.Publish([question], now); exam.PublishCurrentVersion(1);
        db.Exams.Add(exam); db.ExamVersions.Add(examVersion); db.QuestionVersions.Add(question); await db.SaveChangesAsync();
        var store = new EfAiScopeStore(db);
        Assert.NotNull(await store.GetPublishedHomeworkAsync(org, learner, assignment.Id, default));
        Assert.NotNull(await store.GetPublishedExamAsync(org, learner, exam.Id, default));
        if (endClass) (await db.Classes.SingleAsync(x => x.Id == academicClass.ClassId)).Complete(now);
        else (await db.OrganizationPersons.SingleAsync(x => x.Id == learner)).End(now);
        await db.SaveChangesAsync();
        Assert.Null(await store.GetPublishedHomeworkAsync(org, learner, assignment.Id, default));
        Assert.Null(await store.GetPublishedExamAsync(org, learner, exam.Id, default));
        Assert.Null(await store.GetActiveLearnerClassAsync(org, learner, academicClass.ClassId, default));
        Assert.Empty(await store.GetActiveLearnerClassesAsync(org, learner, 10, default));
    }
}
