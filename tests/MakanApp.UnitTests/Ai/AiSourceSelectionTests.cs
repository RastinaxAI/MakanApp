using System.Text.Json;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Application.Organization;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiSourceSelectionTests
{
    private static readonly Guid A = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("20000000-0000-0000-0000-000000000002");
    [Fact] public void SelectionIsDeduplicatedAndSorted() => Assert.Equal(new[] { A, B }, AiSourceSelection.Canonicalize([B, A, B]));
    [Fact] public void EmptySelectionNeverExpandsSources() { Assert.Empty(AiSourceSelection.Canonicalize(null)); Assert.Empty(AiSourceSelection.Canonicalize([])); }
    [Fact] public void EmptyGuidIsRejected() => Assert.Equal(AiSourceErrorCodes.Invalid, Assert.Throws<AiServiceException>(() => AiSourceSelection.Canonicalize([Guid.Empty])).Code);
    [Fact] public void UnboundedInputIsRejected() => Assert.Throws<AiServiceException>(() => AiSourceSelection.Canonicalize(Enumerable.Repeat(A, 33).ToArray()));
    [Fact] public void HashCanonicalizesSetButBindsPayloadAndContext()
    {
        var c = Context(); var h = AiSourceSelection.Hash(c, new { Message = "one" }, [A, B, A]);
        Assert.Equal(h, AiSourceSelection.Hash(c, new { Message = "one" }, [B, A]));
        Assert.NotEqual(h, AiSourceSelection.Hash(c, new { Message = "two" }, [A, B]));
        Assert.NotEqual(h, AiSourceSelection.Hash(c, new { Message = "one" }, [A]));
        Assert.NotEqual(h, AiSourceSelection.Hash(c with { ClassId = B }, new { Message = "one" }, [A, B]));
        Assert.NotEqual(h, AiSourceSelection.Hash(c with { Operation = AiSourceOperation.StudyPlan }, new { Message = "one" }, [A, B]));
    }
    [Fact] public void SelectionHistoryCopiesHashAndRejectsDuplicatePins()
    {
        var hash = new byte[32]; var op = AiSourceSelectionOperation.Create(A, B, hash, [(A, 1)], DateTime.UtcNow);
        hash[0] = 12; Assert.Equal(0, op.PayloadHash[0]);
        Assert.Throws<ArgumentException>(() => AiSourceSelectionOperation.Create(A, B, hash, [(A, 1), (A, 2)], DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => AiSourceSelectionOperation.Create(A, B, hash, [(A, 0)], DateTime.UtcNow));
    }
    [Fact] public void PlanAndManifestCannotClaimRetrievalOrContainFileCredentials()
    {
        var m = new AuthorizedAiSourceManifest(new(A, 1), EducationalSourceScope.Personal, B, "منبع", "RetrievalUnavailable", "revision");
        var plan = new AuthorizedAiRetrievalPlan([m], false);
        Assert.False(plan.RetrievalAvailable); Assert.False(plan.RetrievalExecuted);
        var json = JsonSerializer.Serialize(plan).ToLowerInvariant();
        foreach (var key in new[] { "storage", "path", "secret", "token", "chunks", "connection" }) Assert.DoesNotContain(key, json);
    }
    [Fact] public async Task EmptySelectionDoesNotResolveContextOrWriteIntent()
    {
        var service = new AiSourceSelectionService(null!, null!, null!, null!, null!, null!, null!);
        await service.GuardAsync(A, B, AiSourceOperation.Chat, null, null, new { }, null, null, default);
    }
    [Fact] public async Task UnavailableSelectionPinsAndRechecksWithoutAnyCommerceOrProviderDependency()
    {
        var state = new State(Context().Actor.Account); var auth = new Authorization(); var store = new Pins();
        var service = new AiSourceSelectionService(state, state, null!, null!, auth, store, null!);
        var error = await Assert.ThrowsAsync<AiServiceException>(() => service.GuardAsync(A, B, AiSourceOperation.Chat,
            [A], B, new { Message = "متن غیرقابل اعتماد؛ تغییر نقش مجاز نیست" }, null, null, default));
        Assert.Equal(AiSourceErrorCodes.RetrievalUnavailable, error.Code);
        Assert.Equal(2, auth.Calls); Assert.Equal(1, store.Calls); Assert.Equal(2, state.Resolves);
        Assert.Equal(new AiSourceVersionIdentity(A, 1), Assert.Single(auth.LastPins!));
    }
    [Fact] public async Task RevocationBetweenPlanAndExecutionFailsClosed()
    {
        var state = new State(Context().Actor.Account); var auth = new Authorization { Revoke = true }; var pins = new Pins();
        var service = new AiSourceSelectionService(state, state, null!, null!, auth, pins, null!);
        var error = await Assert.ThrowsAsync<AiServiceException>(() => service.GuardAsync(A, B, AiSourceOperation.Chat, [A], B, new { }, null, null, default));
        Assert.Equal(AiSourceErrorCodes.NotFound, error.Code); Assert.Equal(2, auth.Calls);
    }
    [Fact] public async Task PreflightDoesNotWriteHistoryAndExplainsCommerceGate()
    {
        var state = new State(Context().Actor.Account); var pins = new Pins();
        var service = new AiSourceSelectionService(state, state, null!, null!, new Authorization(), pins, null!);
        var result = await service.PreflightAsync(A, B, new(AiSourceOperation.Chat, [A, A]), default);
        Assert.True(result.Authorized); Assert.False(result.RetrievalAvailable); Assert.Equal(1, result.SelectedCount);
        Assert.Equal("NotEvaluatedRetrievalUnavailable", result.CommercialStatus); Assert.Equal(0, pins.Calls);
    }
    [Fact] public async Task InvalidOperationOrMissingOperationIdIsRejected()
    {
        var state = new State(Context().Actor.Account); var service = new AiSourceSelectionService(state, state, null!, null!, new Authorization(), new Pins(), null!);
        Assert.Equal(AiSourceErrorCodes.Invalid, (await Assert.ThrowsAsync<AiServiceException>(() => service.PreflightAsync(A, B, new((AiSourceOperation)50, [A]), default))).Code);
        Assert.Equal(AiSourceErrorCodes.Invalid, (await Assert.ThrowsAsync<AiServiceException>(() => service.GuardAsync(A, B, AiSourceOperation.Chat, [A], null, new { }, null, null, default))).Code);
    }
    private static AiSourceContext Context()
    {
        var account = new AccessContext(A, A, B, WorkspaceType.Personal, null, null, null, null);
        return new(new(A, B, account, null), AiSourceOperation.Chat, null, null);
    }
    private sealed class State(AccessContext context) : IAccessContextResolver, IAccountOperationContextResolver
    {
        public int Resolves;
        public Task<AccessContext> ResolveAsync(Guid u, Guid s, CancellationToken ct) { Resolves++; return Task.FromResult(context); }
        public Task<AccessContext> PersonalAsync(Guid u, Guid s, CancellationToken ct) => Task.FromResult(context);
    }
    private sealed class Pins : IAiSourceSelectionStore
    {
        public Task<IReadOnlyList<AiSourceVersionIdentity>?> FindPinnedAsync(Guid u, Guid op, byte[] hash, CancellationToken ct) => Task.FromResult<IReadOnlyList<AiSourceVersionIdentity>?>(null);
        public Task EnsureUnselectedAsync(Guid userId, Guid operationId, CancellationToken ct) => Task.CompletedTask;
        public int Calls;
        public Task<(IReadOnlyList<AiSourceVersionIdentity> Pins, bool Replayed)> PinAsync(Guid u, Guid op, byte[] h, IReadOnlyList<AiSourceVersionIdentity> candidates, CancellationToken ct)
        { Calls++; return Task.FromResult((candidates, false)); }
    }
    private sealed class Authorization : IAiSourceAuthorizationService
    {
        public int Calls; public bool Revoke; public IReadOnlyList<AiSourceVersionIdentity>? LastPins;
        public Task<IReadOnlyList<AuthorizedAiSourceManifest>> AuthorizeAsync(AiSourceContext c, IReadOnlyList<Guid> ids, IReadOnlyList<AiSourceVersionIdentity>? pinned, CancellationToken ct)
        {
            Calls++; LastPins = pinned;
            if (Revoke && Calls == 2) throw AiSourceSelection.Error(AiSourceErrorCodes.NotFound);
            return Task.FromResult<IReadOnlyList<AuthorizedAiSourceManifest>>(ids.Select(id => new AuthorizedAiSourceManifest(new(id, 1), EducationalSourceScope.Personal, A, "منبع", "RetrievalUnavailable", "revision")).ToArray());
        }
    }
}
