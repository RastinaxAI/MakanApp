using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingOperationContextTests
{
    [Fact]
    public async Task ExplicitPersonalOperationDoesNotChangeSelectedTeacherContext()
    {
        var selected = Teacher(); var store = new ContextStore { ActiveSession = true };
        var original = new ContextResolver(selected); var resolver = new MessagingOperationContextResolver(original, store, TimeProvider.System);
        var personal = await resolver.ScopeAsync(selected.UserId, selected.SessionId, ConversationScope.Personal, default);
        Assert.Equal(WorkspaceType.Personal, personal.WorkspaceType);
        Assert.Null(personal.OrganizationId); Assert.Null(personal.ActiveRole);
        Assert.Equal(OrganizationRole.Teacher, original.Selected.ActiveRole);
        Assert.Equal(selected, original.Selected);
        Assert.Equal(0, original.Calls);
    }

    [Fact]
    public async Task AuthorizedPersonalResourceUsesTransientAccountContext()
    {
        var selected = Teacher(); var original = new ContextResolver(selected);
        var resolver = new MessagingOperationContextResolver(original, new ContextStore { ActiveSession = true, PersonalParticipant = true }, TimeProvider.System);
        var result = await resolver.ConversationAsync(selected.UserId, selected.SessionId, Guid.NewGuid(), default);
        Assert.Equal(WorkspaceType.Personal, result.WorkspaceType);
        Assert.Equal(selected.UserId, result.UserId); Assert.Equal(selected.SessionId, result.SessionId);
        Assert.Equal(0, original.Calls);
    }

    [Fact]
    public async Task OrganizationResourceRetainsExactSelectedOrganizationAndRole()
    {
        var selected = Teacher(); var original = new ContextResolver(selected);
        var resolver = new MessagingOperationContextResolver(original, new ContextStore { ActiveSession = true }, TimeProvider.System);
        Assert.Same(selected, await resolver.ConversationAsync(selected.UserId, selected.SessionId, Guid.NewGuid(), default));
        Assert.Equal(1, original.Calls);
    }

    [Fact]
    public async Task PersonalContextNeverResurrectsRevokedSession()
    {
        var selected = Teacher(); var resolver = new MessagingOperationContextResolver(new ContextResolver(selected),
            new ContextStore { PersonalParticipant = true, ActiveSession = false }, TimeProvider.System);
        var error = await Assert.ThrowsAsync<IdentityException>(() => resolver.ConversationAsync(selected.UserId, selected.SessionId, Guid.NewGuid(), default));
        Assert.Equal(IdentityErrorCodes.AuthRequired, error.Code);
    }

    [Fact]
    public async Task NonparticipantCannotObtainPersonalResourceProjection()
    {
        var selected = Teacher(); var original = new ContextResolver(selected);
        var resolver = new MessagingOperationContextResolver(original, new ContextStore { ActiveSession = true, PersonalParticipant = false }, TimeProvider.System);
        Assert.Equal(selected, await resolver.ConversationAsync(selected.UserId, selected.SessionId, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task ExplicitOrganizationScopeStillUsesCurrentContextResolver()
    {
        var selected = Teacher(); var original = new ContextResolver(selected);
        var resolver = new MessagingOperationContextResolver(original, new ContextStore { ActiveSession = true }, TimeProvider.System);
        Assert.Equal(selected, await resolver.ScopeAsync(selected.UserId, selected.SessionId, ConversationScope.Organization, default));
        Assert.Equal(1, original.Calls);
    }

    private static AccessContext Teacher()
    {
        var user = Guid.NewGuid(); return new(user, user, Guid.NewGuid(), WorkspaceType.Organization, Guid.NewGuid(), Guid.NewGuid(), OrganizationRole.Teacher, null);
    }

    private sealed class ContextResolver(AccessContext selected) : IAccessContextResolver
    {
        public AccessContext Selected { get; } = selected;
        public int Calls { get; private set; }
        public Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Selected); }
    }

    private sealed class ContextStore : IMessagingOperationContextStore
    {
        public bool ActiveSession { get; init; }
        public bool PersonalParticipant { get; init; }
        public Task<bool> HasActiveAccountSessionAsync(Guid userId, Guid sessionId, DateTime nowUtc, CancellationToken ct) => Task.FromResult(ActiveSession);
        public Task<bool> IsPersonalParticipantAsync(Guid userId, Guid conversationId, CancellationToken ct) => Task.FromResult(PersonalParticipant);
    }
}
