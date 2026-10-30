using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AiSourcesManagerCannotMixTwoAuthorizedOrganizationsAndNoPayerFallbackOccurs()
    {
        using var a = await CreateOfficialScenarioAsync(false); using var b = await CreateOfficialScenarioAsync(false);
        await SourceContract(SubscriptionOwner.Organization(a.OrganizationId), 10); await SourceContract(SubscriptionOwner.Organization(b.OrganizationId), 10);
        var one = await SourcePublish(a.Manager, await SourceCreate(a.Manager, SourceCommand(EducationalSourceScope.Organization, (await SourceUpload(a.Manager, false)).Id)));
        var two = await SourcePublish(b.Manager, await SourceCreate(b.Manager, SourceCommand(EducationalSourceScope.Organization, (await SourceUpload(b.Manager, false)).Id)));
        await factory.CreateMembershipAsync(a.ManagerId, b.OrganizationId, MakanApp.Domain.Organization.OrganizationRole.Manager);
        var before = await AiUsageSnapshot(a.ManagerId); factory.AiService.Reset();
        await AiSourceError(a.Manager, "/api/v1/ai/chat", new SendAiChatCommand("پرسش", null) { SelectedSourceIds = [one.SourceId], OperationId = Guid.NewGuid() }, AiSourceErrorCodes.RetrievalUnavailable, HttpStatusCode.Conflict);
        await AiSourceError(a.Manager, "/api/v1/ai/chat", new SendAiChatCommand("پرسش", null) { SelectedSourceIds = [two.SourceId], OperationId = Guid.NewGuid() }, AiSourceErrorCodes.NotFound, HttpStatusCode.NotFound);
        Assert.Equal(before, await AiUsageSnapshot(a.ManagerId)); Assert.Equal(0, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AiSourcesSqlRevocationAfterPinIsRecheckedBeforeExecutionWithoutUsage()
    {
        factory.AiService.Reset();
        using var s = await CreateOfficialScenarioAsync(false); await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 10);
        var source = await SourcePublish(s.Teacher, await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId)));
        var actor = await ResolveSourceActor(s.TeacherId, s.Teacher); var before = await AiUsageSnapshot(s.TeacherId);
        await using var scope = factory.Services.CreateAsyncScope(); var services = scope.ServiceProvider;
        var gate = new AiSourceSelectionService(services.GetRequiredService<IAccountOperationContextResolver>(), services.GetRequiredService<IAccessContextResolver>(),
            services.GetRequiredService<IStudentContextBuilder>(), services.GetRequiredService<ITeacherClassContextBuilder>(), services.GetRequiredService<IAiSourceAuthorizationService>(),
            new RevokeAfterAiPin(services.GetRequiredService<IAiSourceSelectionStore>(), () => factory.EndTeacherAssignmentAsync(s.TeacherAssignmentId)),
            services.GetRequiredService<IAiScopeStore>());
        var error = await Assert.ThrowsAsync<AiServiceException>(() => gate.GuardAsync(s.TeacherId, actor.SessionId, AiSourceOperation.TeacherActivitySuggestions,
            [source.SourceId], Guid.NewGuid(), new { Objective = "ساختگی" }, s.ClassId, null, default));
        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code); Assert.Equal(before, await AiUsageSnapshot(s.TeacherId)); Assert.Equal(0, factory.AiService.ProviderCallCount);
    }

    [Fact]
    public async Task AiSourcesPersonalPreflightAndRetryRemainProviderFreeAndDoNotChargeQuota()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 10);
        var source = await SourcePublish(client, await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id)));
        Assert.False(source.AiReady); Assert.False(source.Capabilities.CanUseForAi); Assert.Equal("RetrievalUnavailable", source.AiUnavailableReason);
        var before = await AiUsageSnapshot(user.UserId); factory.AiService.Reset();
        using (var preflight = await client.PostAsJsonAsync("/api/v1/ai/sources/preflight", new AiSourcePreflightCommand(AiSourceOperation.Chat, [source.SourceId, source.SourceId]), JsonOptions))
        {
            preflight.EnsureSuccessStatusCode(); var plan = (await preflight.Content.ReadFromJsonAsync<AiSourcePreflightResult>(JsonOptions))!;
            Assert.True(plan.Authorized); Assert.False(plan.RetrievalAvailable); Assert.Equal(1, plan.SelectedCount);
            Assert.Equal(AiSourceErrorCodes.RetrievalUnavailable, plan.ErrorCode); Assert.Equal("NotEvaluatedRetrievalUnavailable", plan.CommercialStatus);
        }
        var command = new SendAiChatCommand("پرسش ساختگی", null) { OperationId = Guid.NewGuid(), SelectedSourceIds = [source.SourceId] };
        var requests = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/ai/chat", command, JsonOptions),
            client.PostAsJsonAsync("/api/v1/ai/chat", command with { SelectedSourceIds = [source.SourceId, source.SourceId] }, JsonOptions));
        foreach (var response in requests) { using (response) { Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); Assert.Contains(AiSourceErrorCodes.RetrievalUnavailable, await response.Content.ReadAsStringAsync()); } }
        Assert.Null(factory.AiService.LastRequest); Assert.Null(factory.AiService.LastRoleRequest);
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        Assert.Equal(before, await AiUsageSnapshot(user.UserId));
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, await db.Set<AiSourceSelectionOperation>().CountAsync(x => x.UserId == user.UserId));
        Assert.Equal(1, await db.Set<AiSourceSelectionPin>().CountAsync(x => x.UserId == user.UserId));
    }

    [Fact]
    public async Task AiSourcesOperationConflictAndVersionPinningSurviveNewPublishedRevision()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 10);
        var first = await SourcePublish(client, await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id)));
        var other = await SourcePublish(client, await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id)));
        var op = Guid.NewGuid(); var request = new SendAiChatCommand("پرسش", null) { OperationId = op, SelectedSourceIds = [first.SourceId] };
        await AiSourceError(client, "/api/v1/ai/chat", request, AiSourceErrorCodes.RetrievalUnavailable, HttpStatusCode.Conflict);
        await AiSourceError(client, "/api/v1/ai/chat", request with { SelectedSourceIds = [other.SourceId] }, AiSourceErrorCodes.OperationConflict, HttpStatusCode.Conflict);
        await AiSourceError(client, "/api/v1/ai/chat", request with { SelectedSourceIds = [] }, AiSourceErrorCodes.OperationConflict, HttpStatusCode.Conflict);
        using var revision = await client.PostAsJsonAsync($"/api/v1/sources/{first.SourceId}/versions",
            new CreateSourceVersionCommand((await SourceUpload(client, true)).Id, "نسخهٔ تازه", null, first.Version, "آزمون"), JsonOptions);
        revision.EnsureSuccessStatusCode(); var v2 = await SourcePublish(client, (await revision.Content.ReadFromJsonAsync<EducationalSourceResult>(JsonOptions))!);
        await AiSourceError(client, "/api/v1/ai/chat", request, AiSourceErrorCodes.RetrievalUnavailable, HttpStatusCode.Conflict);
        var newOp = Guid.NewGuid(); await AiSourceError(client, "/api/v1/ai/chat", request with { OperationId = newOp }, AiSourceErrorCodes.RetrievalUnavailable, HttpStatusCode.Conflict);
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        Assert.Equal(1, (await db.Set<AiSourceSelectionPin>().SingleAsync(x => x.UserId == user.UserId && x.OperationId == op)).Number);
        Assert.Equal(2, (await db.Set<AiSourceSelectionPin>().SingleAsync(x => x.UserId == user.UserId && x.OperationId == newOp)).Number);
        var latest = await db.EducationalSourceVersions.SingleAsync(x => x.SourceId == first.SourceId && x.Number == 2);
        latest.RecordProcessing(SourceProcessingStatus.Failed, "SOURCE_PROCESSING_FAILED"); await db.SaveChangesAsync();
        await AiSourceError(client, "/api/v1/ai/chat", request, AiSourceErrorCodes.RetrievalUnavailable, HttpStatusCode.Conflict);
        await AiSourceError(client, "/api/v1/ai/chat", request with { OperationId = Guid.NewGuid() }, AiSourceErrorCodes.NotReady, HttpStatusCode.Conflict);
        using var archive = await client.PostAsJsonAsync($"/api/v1/sources/{first.SourceId}/archive", new SourceMutationCommand(v2.Version, "آزمون"), JsonOptions);
        archive.EnsureSuccessStatusCode();
        await AiSourceError(client, "/api/v1/ai/chat", request, AiSourceErrorCodes.NotReady, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AiSourcesPersonalForeignAndNonSourceSelectorsAreConcealedBeforeQuota()
    {
        using var owner = factory.CreateClient(); var user = await CreateMessagingUserAsync(owner);
        using var outsider = factory.CreateClient(); var stranger = await CreateMessagingUserAsync(outsider);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 10);
        var source = await SourcePublish(owner, await SourceCreate(owner, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(owner, true)).Id)));
        var before = await AiUsageSnapshot(stranger.UserId);
        foreach (var id in new[] { source.SourceId, source.File!.FileAssetId, Guid.NewGuid() })
            await AiSourceError(outsider, "/api/v1/ai/chat", new SendAiChatCommand("پرسش", null) { OperationId = Guid.NewGuid(), SelectedSourceIds = [id] }, AiSourceErrorCodes.NotFound, HttpStatusCode.NotFound);
        Assert.Equal(before, await AiUsageSnapshot(stranger.UserId));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Set<AiSourceSelectionOperation>().AnyAsync(x => x.UserId == stranger.UserId));
    }

    [Fact]
    public async Task AiSourcesClassContextCannotCrossClassOrOrganizationAndRevocationIsCurrent()
    {
        using var s = await CreateOfficialScenarioAsync(false); await SourceContract(SubscriptionOwner.Organization(s.OrganizationId), 10);
        var source = await SourcePublish(s.Teacher, await SourceCreate(s.Teacher, SourceCommand(EducationalSourceScope.Class, (await SourceUpload(s.Teacher, false)).Id, s.ClassId)));
        using (var ok = await s.Teacher.PostAsJsonAsync("/api/v1/ai/sources/preflight", new AiSourcePreflightCommand(AiSourceOperation.TeacherActivitySuggestions, [source.SourceId], s.ClassId), JsonOptions)) ok.EnsureSuccessStatusCode();
        factory.AiService.Reset();
        await AiSourceError(s.Teacher, $"/api/v1/ai/teachers/classes/{s.ClassId}/activity-suggestions",
            new SuggestTeacherActivitiesCommand("هدف ساختگی", 10, [], null) { OperationId = Guid.NewGuid(), SelectedSourceIds = [source.SourceId] },
            AiSourceErrorCodes.RetrievalUnavailable, HttpStatusCode.Conflict);
        Assert.Equal(0, factory.AiService.ProviderCallCount);
        var second = await factory.CreateAcademicClassAsync(s.OrganizationId);
        var access = await ResolveSourceActor(s.TeacherId, s.Teacher);
        await factory.CreateTeacherAssignmentAsync(s.OrganizationId, second.ClassId, access.Organization!.MembershipId!.Value);
        await AiSourceError(s.Teacher, "/api/v1/ai/sources/preflight", new AiSourcePreflightCommand(AiSourceOperation.TeacherActivitySuggestions, [source.SourceId], second.ClassId), AiSourceErrorCodes.ContextMismatch, HttpStatusCode.Conflict);
        using var other = await CreateOfficialScenarioAsync(false);
        await AiSourceError(other.Teacher, "/api/v1/ai/sources/preflight", new AiSourcePreflightCommand(AiSourceOperation.TeacherActivitySuggestions, [source.SourceId], other.ClassId), AiSourceErrorCodes.NotFound, HttpStatusCode.NotFound);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var studentActor = await ResolveSourceActor(s.StudentId, s.Student);
            var auth = scope.ServiceProvider.GetRequiredService<IAiSourceAuthorizationService>();
            var context = new AiSourceContext(studentActor, AiSourceOperation.HomeworkExplain, s.ClassId, null);
            Assert.Single(await auth.AuthorizeAsync(context, [source.SourceId], null, default));
            await factory.EndEnrollmentAsync(s.EnrollmentId);
            Assert.Equal(AiSourceErrorCodes.NotFound, (await Assert.ThrowsAsync<AiServiceException>(() => auth.AuthorizeAsync(context, [source.SourceId], [new(source.SourceId, 1)], default))).Code);
        }
        await factory.EndTeacherAssignmentAsync(s.TeacherAssignmentId);
        await AiSourceError(s.Teacher, "/api/v1/ai/sources/preflight", new AiSourcePreflightCommand(AiSourceOperation.TeacherActivitySuggestions, [source.SourceId], s.ClassId), AiServiceErrorCodes.ContextNotAllowed, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AiSourcesLibraryRemainsMetadataOnlyEvenForPlatformOwner()
    {
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await factory.ResetPlatformIamAsync(); await factory.BootstrapPlatformOwnerAsync(user.UserId);
        var source = await SourcePublish(client, await SourceCreate(client, SourceCommand(EducationalSourceScope.Library, (await SourceUpload(client, true)).Id)));
        Assert.False(source.Capabilities.CanUseForAi); Assert.Equal("ProductPolicyDisabled", source.AiUnavailableReason);
        await AiSourceError(client, "/api/v1/ai/sources/preflight", new AiSourcePreflightCommand(AiSourceOperation.Chat, [source.SourceId]), AiSourceErrorCodes.NotAllowed, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(SourceProcessingStatus.Pending)] [InlineData(SourceProcessingStatus.Processing)] [InlineData(SourceProcessingStatus.Failed)]
    public async Task AiSourcesProcessingStatesNeverInvokeProvider(SourceProcessingStatus state)
    {
        factory.AiService.Reset();
        using var client = factory.CreateClient(); var user = await CreateMessagingUserAsync(client);
        await SourceContract(SubscriptionOwner.Individual(user.UserId), 10);
        var source = await SourcePublish(client, await SourceCreate(client, SourceCommand(EducationalSourceScope.Personal, (await SourceUpload(client, true)).Id)));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>(); var v = await db.EducationalSourceVersions.SingleAsync(x => x.SourceId == source.SourceId);
            v.RecordProcessing(state, state == SourceProcessingStatus.Failed ? "SOURCE_PROCESSING_FAILED" : null); await db.SaveChangesAsync();
        }
        await AiSourceError(client, "/api/v1/ai/chat", new SendAiChatCommand("پرسش", null) { OperationId = Guid.NewGuid(), SelectedSourceIds = [source.SourceId] }, AiSourceErrorCodes.NotReady, HttpStatusCode.Conflict);
        Assert.Null(factory.AiService.LastRequest);
    }
    private async Task<(int Reservations, int Ledger, int Sources)> AiUsageSnapshot(Guid user)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return (await db.UsageReservations.CountAsync(r => r.ConsumerUserId == user), await db.UsageLedgerEntries.CountAsync(), await db.EducationalSources.CountAsync());
    }
    private static async Task AiSourceError(HttpClient client, string route, object body, string code, HttpStatusCode status)
    {
        using var response = await client.PostAsJsonAsync(route, body, JsonOptions);
        Assert.Equal(status, response.StatusCode); var content = await response.Content.ReadAsStringAsync(); Assert.Contains(code, content);
        foreach (var forbidden in new[] { "StorageKey", "StackTrace", "SigningKey", "Authorization:", "ConnectionString" }) Assert.DoesNotContain(forbidden, content);
    }
    private async Task<SourceActor> ResolveSourceActor(Guid user, HttpClient client)
    {
        using var current = await client.GetAsync("/api/v1/workspaces/current"); current.EnsureSuccessStatusCode();
        var context = (await current.Content.ReadFromJsonAsync<AccessContext>(JsonOptions))!;
        await using var scope = factory.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountOperationContextResolver>().PersonalAsync(user, context.SessionId, default);
        return new(user, context.SessionId, account, context.WorkspaceType == MakanApp.Domain.Organization.WorkspaceType.Organization ? context : null);
    }
    private sealed class RevokeAfterAiPin(IAiSourceSelectionStore inner, Func<Task> revoke) : IAiSourceSelectionStore
    {
        public Task<IReadOnlyList<AiSourceVersionIdentity>?> FindPinnedAsync(Guid u, Guid op, byte[] hash, CancellationToken ct) => inner.FindPinnedAsync(u, op, hash, ct);
        public Task EnsureUnselectedAsync(Guid u, Guid op, CancellationToken ct) => inner.EnsureUnselectedAsync(u, op, ct);
        public async Task<(IReadOnlyList<AiSourceVersionIdentity> Pins, bool Replayed)> PinAsync(Guid u, Guid op, byte[] hash, IReadOnlyList<AiSourceVersionIdentity> pins, CancellationToken ct)
        { var saved = await inner.PinAsync(u, op, hash, pins, ct); await revoke(); return saved; }
    }
}
