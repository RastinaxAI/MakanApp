using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Knowledge;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Identity;
using MakanApp.Infrastructure.Knowledge;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class AccountDeletionAcceptanceTests : IAsyncLifetime
{
    private readonly MakanAppWebApplicationFactory _factory = MakanAppWebApplicationFactory.CreateAccountDeletionProbe();
    private readonly DeletionKnowledgeDouble _knowledge = new();
    private readonly List<HttpClient> _clients = [];
    private static int _phone = 8_900_000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private sealed record Actor(HttpClient Client, Guid Id, Guid Session, string Phone, string RefreshToken, string Username);
    private DateTime Now => _factory.Clock.GetUtcNow().UtcDateTime;

    public Task InitializeAsync() => ((IAsyncLifetime)_factory).InitializeAsync();
    public async Task DisposeAsync()
    {
        foreach (var client in _clients) client.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    private async Task<Actor> ActorAsync(string? phone = null, bool profile = true)
    {
        var client = _factory.CreateClient(); _clients.Add(client);
        phone ??= $"+98938{Interlocked.Increment(ref _phone):D7}";
        var challenge = await Post<RequestOtpResult>(client, "/api/v1/auth/otp/challenges", new RequestOtpCommand(phone));
        var auth = await Post<VerifyOtpResult>(client, "/api/v1/auth/otp/verify", new VerifyOtpCommand(challenge.ChallengeId, phone, _factory.GetLatestOtpCode(phone)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var username = "be22_" + Guid.NewGuid().ToString("N")[..12];
        if (profile)
        {
            using var response = await client.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("نام خصوصی", "آزمون", "هویت خصوصی آزمایشی", username, new DateOnly(2000, 1, 1), "معرفی خصوصی"), Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var session = await Db(db => db.UserSessions.Where(s => s.UserId == auth.User.Id).OrderByDescending(s => s.CreatedAtUtc).Select(s => s.Id).FirstAsync());
        return new(client, auth.User.Id, session, phone, auth.RefreshToken, username);
    }

    private async Task<T> Db<T>(Func<MakanDbContext, Task<T>> action)
    { await using var scope = _factory.Services.CreateAsyncScope(); return await action(scope.ServiceProvider.GetRequiredService<MakanDbContext>()); }
    private async Task Db(Func<MakanDbContext, Task> action)
    { await using var scope = _factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<MakanDbContext>()); }
    private static async Task<T> Post<T>(HttpClient client, string path, object value)
    {
        using var response = await client.PostAsJsonAsync(path, value, Json); Assert.True(response.IsSuccessStatusCode, "HTTP status: " + (int)response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
    private Task<AccountDeletionStatusResult> RequestAsync(Actor actor, Guid? operation = null) =>
        Post<AccountDeletionStatusResult>(actor.Client, "/api/v1/me/deletion-request", new RequestAccountDeletionCommand(operation ?? Guid.NewGuid()));
    private Task<AccountDeletionStatus> Status(Guid request) => Db(db => db.AccountDeletionRequests.Where(r => r.Id == request).Select(r => r.Status).SingleAsync());
    private AccountDeletionProcessor Processor(IServiceProvider services, AccountDeletionOptions? options = null)
    {
        var purger = ActivatorUtilities.CreateInstance<AccountDeletionKnowledgePurger>(services, _knowledge);
        var workflow = ActivatorUtilities.CreateInstance<AccountDeletionWorkflow>(services, purger);
        return options is null
            ? ActivatorUtilities.CreateInstance<AccountDeletionProcessor>(services, workflow, purger)
            : ActivatorUtilities.CreateInstance<AccountDeletionProcessor>(services, workflow, purger, options);
    }
    private async Task<AccountDeletionClaim?> ClaimAsync()
    { await using var scope = _factory.Services.CreateAsyncScope(); return await Processor(scope.ServiceProvider).ClaimNextAsync(default); }
    private async Task ProcessAsync(AccountDeletionClaim claim)
    { await using var scope = _factory.Services.CreateAsyncScope(); await Processor(scope.ServiceProvider).ProcessClaimAsync(claim, default); }
    private async Task OneAsync()
    { var claim = await ClaimAsync(); Assert.NotNull(claim); await ProcessAsync(claim); }
    private async Task DrainAsync()
    {
        for (var i = 0; i < 50; i++) { var claim = await ClaimAsync(); if (claim is null) return; await ProcessAsync(claim); }
        Assert.Fail("صف اجرای آزمون پس از سقف محدود خالی نشد.");
    }

    [Fact]
    public async Task DeletionConcurrentRequestsAreSingleReceiptAndCrossAccountSelectorsDoNotDiscloseIt()
    {
        var owner = await ActorAsync(); var other = await ActorAsync(); var op = Guid.NewGuid();
        var results = await Task.WhenAll(RequestAsync(owner, op), RequestAsync(owner, op));
        Assert.Equal(results[0].Id, results[1].Id); Assert.Equal("Scheduled", results[0].Status);
        Assert.Equal(results[0].Id, (await RequestAsync(owner)).Id);
        using var hidden = await other.Client.GetAsync("/api/v1/me/deletion-request?userId=" + owner.Id);
        Assert.Equal(HttpStatusCode.NoContent, hidden.StatusCode);
        using var detail = await other.Client.GetAsync("/api/admin/v1/privacy/deletion-requests/" + results[0].Id);
        Assert.Equal(HttpStatusCode.Forbidden, detail.StatusCode);
        Assert.Equal(1, await Db(db => db.AccountDeletionRequests.CountAsync(r => r.UserId == owner.Id)));
        Assert.Equal(8, await Db(db => db.AccountDeletionSteps.CountAsync(s => s.RequestId == results[0].Id)));
        Assert.Equal(UserAccountStatus.Active, await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.AccountStatus).SingleAsync()));
    }

    [Fact]
    public async Task DeletionDisabledProcessorCannotClaimOrExecuteEvenAReadyRequest()
    {
        var owner = await ActorAsync(); var request = await RequestAsync(owner);
        await using var scope = _factory.Services.CreateAsyncScope(); var processor = Processor(scope.ServiceProvider, new AccountDeletionOptions());
        Assert.Equal(0, await processor.DispatchBatchAsync(default)); Assert.Null(await processor.ClaimNextAsync(default));
        await processor.ProcessClaimAsync(new(request.Id, Guid.NewGuid()), default);
        Assert.Equal(AccountDeletionStatus.Scheduled, await Status(request.Id)); Assert.Empty(_knowledge.Deletes);
        Assert.Null(await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.DeletionStartedAtUtc).SingleAsync()));
    }

    [Fact]
    public async Task DeletionGraceUsesServerTimeAndDoesNotRunEarly()
    {
        var owner = await ActorAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var store = ActivatorUtilities.CreateInstance<EfAccountDeletionStore>(scope.ServiceProvider, new AccountDeletionOptions { ExecutionEnabled = true, GracePeriodHours = 3 });
            var result = await store.RequestAsync(owner.Id, owner.Session, Guid.NewGuid(), "be22-grace", default);
            Assert.Equal(result.RequestedAtUtc.AddHours(3), result.ScheduledExecutionAtUtc);
        }
        Assert.Null(await ClaimAsync()); _factory.Clock.SetUtcNow(Now.AddHours(3).AddSeconds(1));
        await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Db(db => db.AccountDeletionRequests.Where(r => r.UserId == owner.Id).Select(r => r.Status).SingleAsync()));
    }

    [Fact]
    public async Task DeletionFreezeRevokesRequesterOtherSessionsRefreshPhoneChangeAndPushWithoutLosingSecurityHistory()
    {
        var owner = await ActorAsync(); var second = await ActorAsync(owner.Phone, profile: false);
        var pendingChallenge = await Post<RequestOtpResult>(owner.Client, "/api/v1/auth/otp/challenges", new RequestOtpCommand(owner.Phone));
        var code = _factory.GetLatestOtpCode(owner.Phone);
        await Db(async db =>
        {
            var credential = await db.UserCredentials.SingleAsync(c => c.UserId == owner.Id);
            db.PhoneChangeRequests.Add(PhoneChangeRequest.Prepare(owner.Id, owner.Session, credential, Guid.NewGuid(), "+989351234567", Now, TimeSpan.FromMinutes(2), 5));
            db.PushDeviceRegistrations.Add(MakanApp.Domain.Notification.PushDeviceRegistration.Create(owner.Id, owner.Session,
                MakanApp.Domain.Notification.PushDevicePlatform.Android, "synthetic-device", System.Security.Cryptography.RandomNumberGenerator.GetBytes(32), Now));
            await db.SaveChangesAsync();
        });
        var request = await RequestAsync(owner); await OneAsync();
        foreach (var actor in new[] { owner, second })
        {
            using var me = await actor.Client.GetAsync("/api/v1/me"); Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
            using var refresh = await actor.Client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshSessionCommand(actor.RefreshToken), Json);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }
        using var denied = await owner.Client.PostAsJsonAsync("/api/v1/auth/otp/verify", new VerifyOtpCommand(pendingChallenge.ChallengeId, owner.Phone, code), Json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Db(async db =>
        {
            Assert.Equal(2, await db.UserSessions.CountAsync(s => s.UserId == owner.Id));
            Assert.False(await db.UserSessions.AnyAsync(s => s.UserId == owner.Id && s.RevokedAtUtc == null));
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.RevokedAtUtc == null && db.UserSessions.Any(s => s.Id == t.SessionId && s.UserId == owner.Id)));
            Assert.Equal(PhoneChangeState.Superseded, await db.PhoneChangeRequests.Where(r => r.UserId == owner.Id).Select(r => r.State).SingleAsync());
            Assert.NotNull(await db.PushDeviceRegistrations.Where(d => d.UserId == owner.Id).Select(d => d.RevokedAtUtc).SingleAsync());
        });
        await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        Assert.Null(await Db(db => db.PhoneChangeRequests.Where(r => r.UserId == owner.Id).Select(r => r.CredentialId).SingleAsync()));
    }

    [Fact]
    public async Task DeletionAnonymizesRetainedRootsAllowsPhoneAndFormerUsernameReuseByANewAccount()
    {
        var owner = await ActorAsync(); var person = await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.PersonId).SingleAsync());
        var request = await RequestAsync(owner); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        await Db(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == owner.Id); Assert.Equal(person, user.PersonId);
            Assert.NotNull(user.AnonymizedAtUtc); Assert.Null(user.Bio); Assert.Null(user.ProfilePhotoFileAssetId);
            Assert.StartsWith(User.DeletedUsernamePrefix, user.Username); Assert.False(await db.UserCredentials.AnyAsync(c => c.UserId == owner.Id));
            var identity = await db.Persons.SingleAsync(p => p.Id == person); Assert.Equal(Person.DeletedDisplayName, identity.DisplayName); Assert.Null(identity.DateOfBirth);
        });
        var replacement = await ActorAsync(owner.Phone); Assert.NotEqual(owner.Id, replacement.Id);
        using var reused = await replacement.Client.PatchAsJsonAsync("/api/v1/me/profile", new CompleteProfileCommand("نام جدید", "حساب تازه", null, owner.Username), Json);
        Assert.Equal(HttpStatusCode.OK, reused.StatusCode);
        Assert.Equal(2, await Db(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task DeletionDoesNotRequireOrFabricateACompletedProfile()
    {
        var owner = await ActorAsync(profile: false); var request = await RequestAsync(owner); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        await Db(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == owner.Id);
            Assert.Null(user.PersonId); Assert.Null(user.ProfileCompletedAtUtc); Assert.NotNull(user.AnonymizedAtUtc);
            Assert.StartsWith(User.DeletedUsernamePrefix, user.Username); Assert.False(await db.Persons.AnyAsync());
            Assert.False(await db.UserCredentials.AnyAsync(c => c.UserId == owner.Id));
        });
    }

    [Fact]
    public async Task DeletionConcurrentWorkersHaveOneClaimAndEightSingleCompletionAudits()
    {
        var owner = await ActorAsync(); var request = await RequestAsync(owner);
        var claims = await Task.WhenAll(ClaimAsync(), ClaimAsync()); var claim = Assert.Single(claims, c => c is not null);
        await ProcessAsync(claim!); await ProcessAsync(claim!); await DrainAsync();
        Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
        await Db(async db =>
        {
            Assert.Equal(8, await db.AdminAuditLogs.CountAsync(a => a.TargetId == request.Id.ToString() && a.Action == "privacy.DeletionStepCompleted"));
            Assert.Equal(1, await db.AdminAuditLogs.CountAsync(a => a.TargetId == request.Id.ToString() && a.Action == "privacy.DeletionCompleted"));
            Assert.All(await db.AccountDeletionSteps.Where(s => s.RequestId == request.Id).ToArrayAsync(), s => Assert.Equal(AccountDeletionStepStatus.Completed, s.Status));
        });
    }

    [Fact]
    public async Task DeletionExpiredClaimIsRecoveredAndTheAbandonedWorkerCannotAcknowledge()
    {
        var owner = await ActorAsync(); var request = await RequestAsync(owner); var abandoned = await ClaimAsync(); Assert.NotNull(abandoned);
        _factory.Clock.SetUtcNow(Now.Add(AccountDeletionProcessor.ClaimLease).AddSeconds(1));
        var recovered = await ClaimAsync(); Assert.NotNull(recovered); Assert.NotEqual(abandoned.Token, recovered.Token);
        await ProcessAsync(abandoned); Assert.Null(await Db(db => db.Users.Where(u => u.Id == owner.Id).Select(u => u.DeletionStartedAtUtc).SingleAsync()));
        await ProcessAsync(recovered); await DrainAsync(); Assert.Equal(AccountDeletionStatus.Completed, await Status(request.Id));
    }

    private sealed class DeletionKnowledgeDouble : IAiKnowledgeClient
    {
        public List<KnowledgeSourceVersion> Deletes { get; } = [];
        public List<string> CommandIds { get; } = [];
        public HashSet<string> RemoteEffects { get; } = [];
        public Func<Task>? BeforeResponse { get; set; }
        public bool Unavailable { get; set; }
        public int? UnavailableVersion { get; set; }
        public bool NotIngested { get; set; }
        public string State { get; set; } = "Deleted";
        public async Task<KnowledgeProcessingStatus> DeleteAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct)
        {
            Deletes.Add(source); CommandIds.Add(request.RequestId);
            if (Unavailable || source.Version == UnavailableVersion) throw new AiKnowledgeException(KnowledgeErrorCodes.Unavailable, 503);
            if (NotIngested) throw new AiKnowledgeException(KnowledgeErrorCodes.NotIngested, 409);
            RemoteEffects.Add(request.RequestId);
            if (BeforeResponse is not null) await BeforeResponse();
            return new(source.SourceId, source.Version, State, DateTimeOffset.UtcNow);
        }
        public Task<KnowledgeIngestResult> IngestAsync(KnowledgeIngestMetadata metadata, ReadOnlyMemory<byte> content, CancellationToken ct) => throw new NotSupportedException();
        public Task<KnowledgeProcessingStatus> StatusAsync(KnowledgeSourceVersion source, CancellationToken ct, string? correlationId = null) => throw new NotSupportedException();
        public Task<KnowledgeProcessingStatus> SupersedeAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<KnowledgeProcessingStatus> ReindexAsync(KnowledgeSourceVersion source, KnowledgeCommandRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<KnowledgeProcessingStatus> WaitForCompletionAsync(KnowledgeSourceVersion source, CancellationToken ct, string? correlationId = null) => throw new NotSupportedException();
        public Task<KnowledgeRetrieveResponse> RetrieveAsync(KnowledgeRetrieveRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<KnowledgeRagResponse> AnswerAsync(KnowledgeRagRequest request, KnowledgeRetrieveResponse evidence, CancellationToken ct) => throw new NotSupportedException();
    }
}
