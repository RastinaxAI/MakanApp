using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Realtime;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class AccountInboxSummaryTests
{
    [Fact]
    public async Task SummaryRejectsInactiveSessionBeforeResourceLookup()
    {
        var store = new SummaryStore { SessionActive = false };
        var error = await Assert.ThrowsAsync<IdentityException>(() => Service(store)
            .ReadSummaryAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Equal(IdentityErrorCodes.AuthRequired, error.Code);
        Assert.Equal(0, store.Reads);
    }

    [Fact]
    public async Task SummaryRechecksSessionAfterResourceLookup()
    {
        var store = new SummaryStore { RevokeDuringRead = true };
        var error = await Assert.ThrowsAsync<IdentityException>(() => Service(store)
            .ReadSummaryAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), default));
        Assert.Equal(IdentityErrorCodes.AuthRequired, error.Code);
        Assert.Equal(1, store.Reads);
        Assert.Equal(2, store.SessionChecks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SummaryConcealsEmptyOrMissingResourceWithoutConsultingSelectedWorkspace(bool empty)
    {
        var store = new SummaryStore();
        var error = await Assert.ThrowsAsync<MessagingException>(() => Service(store)
            .ReadSummaryAsync(Guid.NewGuid(), Guid.NewGuid(), empty ? Guid.Empty : Guid.NewGuid(), default));
        Assert.Equal(MessagingErrorCodes.ConversationNotFound, error.Code);
        Assert.Equal(empty ? 0 : 1, store.Reads);
    }

    private static AccountInboxService Service(SummaryStore store) => new(store,
        new MessagingOperationContextResolver(new ForbiddenWorkspace(), store, TimeProvider.System),
        new UnusedCursor(), TimeProvider.System);

    private sealed class ForbiddenWorkspace : IAccessContextResolver
    {
        public Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Summary نباید از Workspace انتخاب‌شده مجوز بگیرد.");
    }

    private sealed class UnusedCursor : IRealtimeCursorProtector
    {
        public string Protect(string value) => throw new NotSupportedException();
        public string Unprotect(string value) => throw new NotSupportedException();
    }

    private sealed class SummaryStore : IAccountInboxStore, IMessagingOperationContextStore
    {
        public bool SessionActive { get; set; } = true;
        public bool RevokeDuringRead { get; init; }
        public int Reads { get; private set; }
        public int SessionChecks { get; private set; }
        public Task<ConversationSummaryStoreRecord?> ReadConversationSummaryAsync(Guid userId, Guid conversationId, CancellationToken ct)
        {
            Reads++;
            if (RevokeDuringRead) SessionActive = false;
            return Task.FromResult<ConversationSummaryStoreRecord?>(null);
        }
        public Task<bool> HasActiveAccountSessionAsync(Guid userId, Guid sessionId, DateTime nowUtc, CancellationToken ct)
        { SessionChecks++; return Task.FromResult(SessionActive); }
        public Task<bool> IsPersonalParticipantAsync(Guid userId, Guid conversationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ConversationSummaryStoreRecord>> ReadAccountInboxAsync(Guid userId,
            ConversationListFilter filter, string? search, int limit, AccountInboxPosition? position, CancellationToken ct) => throw new NotSupportedException();
    }
}
