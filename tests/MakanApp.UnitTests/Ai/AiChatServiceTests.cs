using MakanApp.Application.Ai;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiChatServiceTests
{
    public static TheoryData<OrganizationRole> AllowedRoles => new()
    {
        OrganizationRole.Student,
        OrganizationRole.Parent,
        OrganizationRole.Teacher,
        OrganizationRole.Manager
    };

    [Theory]
    [MemberData(nameof(AllowedRoles))]
    public async Task ChatAsync_UsesServerResolvedRoleAndOrganization(OrganizationRole role)
    {
        var childId = role == OrganizationRole.Parent ? Guid.NewGuid() : (Guid?)null;
        var context = OrganizationContext(role, childId);
        var resolver = new StubContextResolver(context);
        var scopeStore = new StubScopeStore { LearnerId = Guid.NewGuid() };
        var client = new RecordingClient();
        var service = new AiChatService(resolver, scopeStore, client);

        var result = await service.ChatAsync(
            context.UserId,
            context.SessionId,
            "corr-1",
            new SendAiChatCommand("  پرسش  ", null),
            CancellationToken.None);

        Assert.Equal("پاسخ", result.Answer.Text);
        Assert.NotNull(client.Request);
        Assert.Equal(role.ToString(), client.Request!.Actor.Role);
        Assert.Equal(context.UserId.ToString("D"), client.Request.Actor.UserId);
        Assert.Equal(context.OrganizationId!.Value.ToString("D"), client.Request.Scope.OrganizationId);
        Assert.Equal(context.OrganizationId.Value.ToString("D"), client.Request.Scope.SchoolId);
        Assert.Equal("پرسش", client.Request.Message.Text);
        Assert.Empty(client.Request.Context.Subjects);
        Assert.Null(client.Request.Scope.ClassId);
        var expectedLearner = role == OrganizationRole.Student ? scopeStore.LearnerId : childId;
        Assert.Equal(expectedLearner?.ToString("D"), client.Request.Scope.StudentId);
    }

    [Fact]
    public async Task ChatAsync_PersonalWorkspace_IsDeniedBeforeClientCall()
    {
        var context = new AccessContext(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), WorkspaceType.Personal,
            null, null, null, null);
        var client = new RecordingClient();
        var service = new AiChatService(
            new StubContextResolver(context),
            new StubScopeStore(),
            client);

        var error = await Assert.ThrowsAsync<AiServiceException>(() => service.ChatAsync(
            context.UserId,
            context.SessionId,
            "corr-1",
            new SendAiChatCommand("پرسش", null),
            CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
        Assert.Null(client.Request);
    }

    [Fact]
    public async Task ChatAsync_StudentWithoutActiveOrganizationPerson_IsDenied()
    {
        var context = OrganizationContext(OrganizationRole.Student);
        var client = new RecordingClient();
        var service = new AiChatService(
            new StubContextResolver(context),
            new StubScopeStore(),
            client);

        var error = await Assert.ThrowsAsync<AiServiceException>(() => service.ChatAsync(
            context.UserId,
            context.SessionId,
            "corr-1",
            new SendAiChatCommand("پرسش", null),
            CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.ContextNotAllowed, error.Code);
        Assert.Null(client.Request);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ChatAsync_InvalidMessage_IsRejected(string message)
    {
        var context = OrganizationContext(OrganizationRole.Manager);
        var client = new RecordingClient();
        var service = new AiChatService(
            new StubContextResolver(context),
            new StubScopeStore(),
            client);

        var error = await Assert.ThrowsAsync<AiServiceException>(() => service.ChatAsync(
            context.UserId,
            context.SessionId,
            "corr-1",
            new SendAiChatCommand(message, null),
            CancellationToken.None));

        Assert.Equal(AiServiceErrorCodes.InvalidRequest, error.Code);
        Assert.Null(client.Request);
    }

    private static AccessContext OrganizationContext(
        OrganizationRole role,
        Guid? subjectId = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        WorkspaceType.Organization,
        Guid.NewGuid(),
        Guid.NewGuid(),
        role,
        subjectId);

    private sealed class StubContextResolver(AccessContext context) : IAccessContextResolver
    {
        public Task<AccessContext> ResolveAsync(
            Guid userId,
            Guid sessionId,
            CancellationToken cancellationToken) => Task.FromResult(context);
    }

    private sealed class StubScopeStore : IAiScopeStore
    {
        public Guid? LearnerId { get; init; }

        public Task<Guid?> GetActiveLearnerOrganizationPersonIdAsync(
            Guid organizationId,
            Guid userId,
            CancellationToken cancellationToken) => Task.FromResult(LearnerId);
    }

    private sealed class RecordingClient : IAiServiceClient
    {
        public AiChatRequest? Request { get; private set; }

        public Task<AiChatResponse> ChatAsync(
            AiChatRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new AiChatResponse(
                request.RequestId,
                request.CorrelationId,
                request.ConversationId,
                new AiMessage("پاسخ"),
                [],
                [],
                [],
                [],
                new AiResponseMetadata("test", 1)));
        }
    }
}
