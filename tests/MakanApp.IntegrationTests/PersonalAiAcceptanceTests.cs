using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Intelligence;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    public async Task PersonalChatRoleAndOrganizationSwitchesNeverChangeItsScopeOrAnotherSession()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 20));
        var a = await factory.CreateOrganizationAsync("مدرسه اول آزمون Personal");
        var b = await factory.CreateOrganizationAsync("مدرسه دوم آزمون Personal");
        var first = await factory.CreateMembershipAsync(actor.UserId, a, OrganizationRole.Student);
        await WithDb(async db =>
        {
            db.RoleAssignments.Add(RoleAssignment.CreateActive(first.MembershipId, OrganizationRole.Teacher, DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
        var second = await factory.CreateMembershipAsync(actor.UserId, b, OrganizationRole.Teacher);
        foreach (var workspace in new[] { (first.MembershipId, OrganizationRole.Student), (first.MembershipId, OrganizationRole.Teacher), (second.MembershipId, OrganizationRole.Teacher) })
        {
            await SelectAsync(actor, workspace.MembershipId, workspace.Item2);
            using var response = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش شخصی ساختگی", null), Json);
            response.EnsureSuccessStatusCode();
            Assert.Equal(new AiActor(actor.UserId.ToString("D"), "Personal"), factory.AiService.LastRequest!.Actor);
            Assert.Equal(new AiScope(null, null, null, null), factory.AiService.LastRequest.Scope);
        }
        var other = await AnotherDeviceAsync(actor);
        using (var selected = await other.Client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Organization, Guid.NewGuid(), first.MembershipId, OrganizationRole.Teacher), Json)) selected.EnsureSuccessStatusCode();
        other.Client.DefaultRequestHeaders.Add("X-AI-Payer", "Individual");
        using (var organization = await other.Client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش سازمانی ساختگی", null), Json)) organization.EnsureSuccessStatusCode();
        Assert.Equal("Teacher", factory.AiService.LastRequest!.Actor.Role); Assert.Equal(a.ToString("D"), factory.AiService.LastRequest.Scope.OrganizationId);
        using (var personal = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("ادامه شخصی ساختگی", null), Json)) personal.EnsureSuccessStatusCode();
        Assert.Equal("Personal", factory.AiService.LastRequest!.Actor.Role); Assert.Equal(new AiScope(null, null, null, null), factory.AiService.LastRequest.Scope);
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.Conflict, CommerceErrorCodes.SubscriptionInactive)]
    [InlineData("exhausted", HttpStatusCode.Conflict, CommerceErrorCodes.QuotaExceeded)]
    [InlineData("organization", HttpStatusCode.Forbidden, CommerceErrorCodes.ContextNotAllowed)]
    public async Task PersonalChatCommercialDenialNeverInvokesOrReserves(string mode, HttpStatusCode status, string code)
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        if (mode != "missing") await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId),
            await PlanAsync(SubscriptionOwnerType.Individual, ai: mode == "exhausted" ? 0 : 10));
        if (mode == "organization") actor.Client.DefaultRequestHeaders.Add("X-AI-Payer", "Organization");
        var operation = Guid.NewGuid();
        using var response = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", new SendAiChatCommand("پرسش ساختگی", null) { OperationId = operation }, Json);
        Assert.Equal(status, response.StatusCode); Assert.Equal(code, await ProblemAsync(response));
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        await WithDb(async db => Assert.False(await db.UsageReservations.AnyAsync(r => r.OperationId == operation)));
    }

    [Fact]
    public async Task PersonalChatExplicitTrialInvokesWithoutSubscriptionAndCompletedRetryNeverChargesAgain()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        actor.Client.DefaultRequestHeaders.Add("X-AI-Payer", "FreeTrial");
        var command = new SendAiChatCommand("پرسش ساختگی trial", null) { OperationId = Guid.NewGuid() };
        using (var response = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", command, Json)) response.EnsureSuccessStatusCode();
        using (var replay = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", command, Json))
        { Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal(AiInvocationErrorCodes.Completed, await ProblemAsync(replay)); }
        Assert.Equal(1, factory.AiService.ProviderCallCount); Assert.Equal("Personal", factory.AiService.LastRequest!.Actor.Role);
        await WithDb(async db =>
        {
            var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == command.OperationId);
            Assert.True(reservation.IsTrial); Assert.Null(reservation.SubscriptionId); Assert.Equal(UsageReservationStatus.Consumed, reservation.Status);
        });
    }

    [Fact]
    public async Task PersonalChatConcurrentHttpRetryInvokesAndConsumesOnlyOnce()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        var command = new SendAiChatCommand("پرسش هم‌زمان ساختگی", null) { OperationId = Guid.NewGuid() };
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => actor.Client.PostAsJsonAsync("/api/v1/ai/chat", command, Json)));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict)); Assert.Equal(1, factory.AiService.ProviderCallCount);
        }
        finally { foreach (var response in responses) response.Dispose(); }
        await WithDb(async db => Assert.Equal(UsageReservationStatus.Consumed,
            (await db.UsageReservations.SingleAsync(r => r.OperationId == command.OperationId)).Status));
    }

    [Fact]
    public async Task PersonalChatTimeoutKeepsUnknownAndSameHttpIntentNeverRetriesProvider()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        factory.AiService.FailNext(new AiServiceException(AiServiceErrorCodes.ServiceTimeout, "timeout ساختگی"));
        var command = new SendAiChatCommand("پرسش timeout ساختگی", null) { OperationId = Guid.NewGuid() };
        using (var failed = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", command, Json)) Assert.Equal(HttpStatusCode.GatewayTimeout, failed.StatusCode);
        using (var replay = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat", command, Json))
        { Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode); Assert.Equal(AiInvocationErrorCodes.Unknown, await ProblemAsync(replay)); }
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await WithDb(async db => Assert.Equal(UsageReservationStatus.Unknown,
            (await db.UsageReservations.SingleAsync(r => r.OperationId == command.OperationId)).Status));
    }

    private static async Task ChoosePersonalAsync(TestActor actor)
    {
        using var response = await actor.Client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Personal, Guid.NewGuid()), Json);
        response.EnsureSuccessStatusCode();
    }

    // این پذیرش مرز داخلی Executor و SQL است؛ پاسخ ساختگی، اثبات سازگاری wire شخصی با AI مستقر نیست.
    private async Task<AiChatResponse> ExecutePersonalIntentAsync(TestActor actor, Guid operation,
        Func<PersonalAiChatIntent, CancellationToken, Task<AiChatResponse>> invoke, AiPayerSelection? payer = null,
        string message = "قصد ساختگی آزمون", Func<CancellationToken, Task>? reauthorize = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = await scope.ServiceProvider.GetRequiredService<AssistantContextResolver>().ResolveAsync(actor.UserId, actor.SessionId, default);
        var intent = PersonalAiChatIntent.Create(actor.UserId, actor.SessionId, context, "personal-acceptance", new(message, null));
        return await scope.ServiceProvider.GetRequiredService<IAiInvocationExecutor>().ExecuteAsync(actor.UserId, actor.SessionId,
            intent, invoke, reauthorize ?? (_ => Task.CompletedTask), new(operation, payer), default);
    }

    private static Task<AiChatResponse> PersonalSyntheticResponse(PersonalAiChatIntent intent, CancellationToken ct) =>
        Task.FromResult(new AiChatResponse(intent.RequestId, intent.CorrelationId, null, new("پاسخ ساختگی داخلی"), [], [], [], [], new("test-only", 1)));

    [Fact]
    public async Task PersonalAiOrdinaryAccountInvokesOfficialPersonalContractAndConsumesIndividualOnce()
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        var operation = Guid.NewGuid();
        using var response = await actor.Client.PostAsJsonAsync("/api/v1/ai/chat",
            new SendAiChatCommand("پرسش عادی", null) { OperationId = operation }, Json);
        response.EnsureSuccessStatusCode();
        var sent = Assert.IsType<AiChatRequest>(factory.AiService.LastRequest);
        Assert.Equal(new AiActor(actor.UserId.ToString("D"), "Personal"), sent.Actor);
        Assert.Equal(new AiScope(null, null, null, null), sent.Scope);
        Assert.Equal(AiAuthorizedContext.Empty, sent.Context);
        Assert.Equal(1, factory.AiService.ProviderCallCount);
        await WithDb(async db =>
        {
            Assert.False(await db.Memberships.AnyAsync(m => m.UserId == actor.UserId));
            var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == operation);
            Assert.Equal(UsageReservationStatus.Consumed, reservation.Status);
            Assert.Equal(actor.UserId, reservation.OwnerId); Assert.False(reservation.IsTrial);
        });
    }

    [Theory]
    [InlineData("students/analyze")]
    [InlineData("study-plan")]
    [InlineData("homeworks/11111111-1111-4111-8111-111111111111/explain")]
    [InlineData("exams/11111111-1111-4111-8111-111111111111/prepare")]
    [InlineData("parents/child-summary")]
    [InlineData("teachers/classes/11111111-1111-4111-8111-111111111111/summary")]
    [InlineData("teachers/classes/11111111-1111-4111-8111-111111111111/students/22222222-2222-4222-8222-222222222222/insight")]
    [InlineData("teachers/classes/11111111-1111-4111-8111-111111111111/activity-suggestions")]
    [InlineData("managers/school-summary")]
    [InlineData("managers/classes/11111111-1111-4111-8111-111111111111/insight")]
    [InlineData("managers/classes/11111111-1111-4111-8111-111111111111/students/22222222-2222-4222-8222-222222222222/insight")]
    public async Task PersonalAiIndividualSubscriptionNeverGrantsAnyEducationalRoleEndpoint(string route)
    {
        factory.AiService.Reset(); var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        object body = route == "study-plan" ? new { goals = new[] { "هدف ساختگی" }, availableMinutes = 30 }
            : route.StartsWith("homeworks/", StringComparison.Ordinal) ? new { question = "پرسش ساختگی" }
            : route.EndsWith("activity-suggestions", StringComparison.Ordinal) ? new { objective = "هدف ساختگی", availableMinutes = 30, materials = Array.Empty<string>() } : new { };
        using var response = await actor.Client.PostAsJsonAsync("/api/v1/ai/" + route, body, Json);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        await WithDb(async db => Assert.False(await db.UsageReservations.AnyAsync(r => r.ConsumerUserId == actor.UserId)));
    }

    [Fact]
    public async Task PersonalAiInternalExecutorUsesIndividualAndReplaysWithoutSecondEffect()
    {
        var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 1));
        var operation = Guid.NewGuid(); var calls = 0;
        Task<AiChatResponse> Invoke(PersonalAiChatIntent intent, CancellationToken ct)
        { Interlocked.Increment(ref calls); Assert.Null(intent.Scope.OrganizationId); return PersonalSyntheticResponse(intent, ct); }
        await ExecutePersonalIntentAsync(actor, operation, Invoke);
        var repeat = await Assert.ThrowsAsync<AiServiceException>(() => ExecutePersonalIntentAsync(actor, operation, Invoke));
        Assert.Equal(AiInvocationErrorCodes.Completed, repeat.Code); Assert.Equal(1, calls);
        var conflict = await Assert.ThrowsAsync<CommerceException>(() => ExecutePersonalIntentAsync(actor, operation, Invoke, message: "قصد متفاوت"));
        Assert.Equal(CommerceErrorCodes.UsageConflict, conflict.Code);
        await WithDb(async db =>
        {
            var reservation = await db.UsageReservations.SingleAsync(r => r.OperationId == operation);
            Assert.Equal(UsageReservationStatus.Consumed, reservation.Status); Assert.False(reservation.IsTrial);
            Assert.Equal(SubscriptionOwnerType.Individual, reservation.OwnerType); Assert.Equal(actor.UserId, reservation.OwnerId);
            Assert.Equal(CommerceContextType.Personal, reservation.ContextType); Assert.Equal(actor.UserId, reservation.ContextId);
        });
    }

    [Fact]
    public async Task PersonalAiConcurrentInternalDuplicateCallsDelegateOnlyOnce()
    {
        var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Guid.NewGuid(); var calls = 0;
        async Task<AiChatResponse> Invoke(PersonalAiChatIntent intent, CancellationToken ct)
        { Interlocked.Increment(ref calls); entered.TrySetResult(); await release.Task.WaitAsync(ct); return await PersonalSyntheticResponse(intent, ct); }
        var first = ExecutePersonalIntentAsync(actor, operation, Invoke);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var duplicate = await Assert.ThrowsAsync<AiServiceException>(() => ExecutePersonalIntentAsync(actor, operation, Invoke));
            Assert.Equal(AiInvocationErrorCodes.InProgress, duplicate.Code);
        }
        finally { release.TrySetResult(); }
        await first; Assert.Equal(1, calls);
        await WithDb(async db => Assert.Equal(1, await db.UsageReservations.CountAsync(r => r.OperationId == operation)));
    }

    [Fact]
    public async Task PersonalAiUnknownOutcomeDoesNotRetryAndPreInvocationRejectionReleases()
    {
        var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        var operation = Guid.NewGuid(); var calls = 0;
        Task<AiChatResponse> Timeout(PersonalAiChatIntent intent, CancellationToken ct)
        { calls++; throw new AiServiceException(AiServiceErrorCodes.ServiceTimeout, "خطای ساختگی بدون دادهٔ خصوصی"); }
        await Assert.ThrowsAsync<AiServiceException>(() => ExecutePersonalIntentAsync(actor, operation, Timeout));
        var repeat = await Assert.ThrowsAsync<AiServiceException>(() => ExecutePersonalIntentAsync(actor, operation, Timeout));
        Assert.Equal(AiInvocationErrorCodes.Unknown, repeat.Code); Assert.Equal(1, calls);
        var rejected = Guid.NewGuid();
        await Assert.ThrowsAsync<AiServiceException>(() => ExecutePersonalIntentAsync(actor, rejected, PersonalSyntheticResponse,
            reauthorize: _ => throw new AiServiceException(AiServiceErrorCodes.ContextNotAllowed, "عدم مجوز ساختگی")));
        await WithDb(async db =>
        {
            Assert.Equal(UsageReservationStatus.Unknown, (await db.UsageReservations.SingleAsync(r => r.OperationId == operation)).Status);
            Assert.Equal(UsageReservationStatus.Released, (await db.UsageReservations.SingleAsync(r => r.OperationId == rejected)).Status);
        });
    }

    [Fact]
    public async Task PersonalAiExplicitTrialUsesExistingLedgerAndOrganizationPayerIsRejected()
    {
        var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        var operation = Guid.NewGuid();
        await ExecutePersonalIntentAsync(actor, operation, PersonalSyntheticResponse, AiPayerSelection.FreeTrial);
        var deniedOperation = Guid.NewGuid();
        var denied = await Assert.ThrowsAsync<CommerceException>(() => ExecutePersonalIntentAsync(actor, deniedOperation,
            PersonalSyntheticResponse, AiPayerSelection.Organization));
        Assert.Equal(CommerceErrorCodes.ContextNotAllowed, denied.Code);
        await WithDb(async db =>
        {
            var trial = await db.UsageReservations.SingleAsync(r => r.OperationId == operation);
            Assert.True(trial.IsTrial); Assert.Null(trial.SubscriptionId); Assert.Equal(actor.UserId, trial.OwnerId);
            Assert.Equal(UsageReservationStatus.Consumed, trial.Status);
            Assert.False(await db.UsageReservations.AnyAsync(r => r.OperationId == deniedOperation));
        });
    }

    [Fact]
    public async Task PersonalAiNoSubscriptionDoesNotSilentlyUseTrialAndExhaustedQuotaNeverInvokes()
    {
        var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        var missing = await Assert.ThrowsAsync<CommerceException>(() => ExecutePersonalIntentAsync(actor, Guid.NewGuid(), PersonalSyntheticResponse));
        Assert.Equal(CommerceErrorCodes.SubscriptionInactive, missing.Code);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual, ai: 0));
        var calls = 0;
        Task<AiChatResponse> Invoke(PersonalAiChatIntent intent, CancellationToken ct)
        { calls++; return PersonalSyntheticResponse(intent, ct); }
        var exhausted = await Assert.ThrowsAsync<CommerceException>(() => ExecutePersonalIntentAsync(actor, Guid.NewGuid(), Invoke));
        Assert.Equal(CommerceErrorCodes.QuotaExceeded, exhausted.Code); Assert.Equal(0, calls);
    }

    [Fact]
    public async Task PersonalAiAssistantRemainsPersonalAcrossSchoolSwitchAndSessionsAreIndependent()
    {
        var actor = await ActorAsync(); await ChoosePersonalAsync(actor);
        await SubscribeAsync(SubscriptionOwner.Individual(actor.UserId), await PlanAsync(SubscriptionOwnerType.Individual));
        var organization = await factory.CreateOrganizationAsync("Workspace مستقل از Assistant شخصی");
        var member = await factory.CreateMembershipAsync(actor.UserId, organization, OrganizationRole.Teacher);
        await SelectAsync(actor, member.MembershipId, OrganizationRole.Teacher);
        var other = await AnotherDeviceAsync(actor);
        using (var chosen = await other.Client.PutAsJsonAsync("/api/v1/ai/assistant-context",
            new SelectAssistantContextCommand(AssistantDataContextKind.Organization, Guid.NewGuid(), member.MembershipId, OrganizationRole.Teacher), Json))
            chosen.EnsureSuccessStatusCode();
        await ExecutePersonalIntentAsync(actor, Guid.NewGuid(), (intent, ct) =>
        {
            Assert.Null(intent.Scope.OrganizationId); Assert.Null(intent.Scope.ClassId); Assert.Null(intent.Scope.StudentId);
            return PersonalSyntheticResponse(intent, ct);
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<AssistantContextResolver>();
        Assert.Equal(WorkspaceType.Personal, (await resolver.ResolveAsync(actor.UserId, actor.SessionId, default)).WorkspaceType);
        Assert.Equal(organization, (await resolver.ResolveAsync(other.UserId, other.SessionId, default)).OrganizationId);
        Assert.Equal(organization, (await GetAsync<AccessContext>(actor.Client, "/api/v1/workspaces/current")).OrganizationId);
    }
}
