using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using MakanApp.Api.Realtime;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task RealtimeAudienceUsesBoundedKeysetPagesWithoutLosingAuthorizedSessions()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var notification = await ReadP0ConversationNotificationAsync(setup.FirstClient, setup.ConversationId);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        Guid[] expected;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            for (var index = 0; index < MessagingRealtimeAudienceResolver.CandidatePageSize + 5; index++)
                db.UserSessions.Add(UserSession.Create(setup.First.UserId, RandomNumberGenerator.GetBytes(32), now, now.AddHours(1)));
            await db.SaveChangesAsync();
            expected = await db.UserSessions.AsNoTracking().Where(session => session.UserId == setup.First.UserId &&
                session.RevokedAtUtc == null && session.ExpiresAtUtc > now && session.AbsoluteExpiresAtUtc > now)
                .Select(session => session.Id).ToArrayAsync();
        }
        var observer = new AudiencePageObserver();
        await using var connectionScope = factory.Services.CreateAsyncScope();
        var connectionString = connectionScope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString()!;
        await using var audienceDb = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(observer).Options);
        var resolver = new MessagingRealtimeAudienceResolver(audienceDb, factory.Services.GetRequiredService<IServiceScopeFactory>());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var actual = await resolver.GetActiveSessionIdsAsync(notification, setup.First.UserId, now, deadline.Token).ToTestArrayAsync();
        Assert.True(expected.Length > MessagingRealtimeAudienceResolver.CandidatePageSize);
        Assert.Equal(expected.Order(), actual.Order());
        Assert.Equal(actual.Count, actual.Distinct().Count());
        Assert.InRange(observer.PageCount, 2, 3);
        Assert.All(observer.Limits, limit => Assert.Equal(MessagingRealtimeAudienceResolver.CandidatePageSize, limit));
    }

    private sealed class AudiencePageObserver : DbCommandInterceptor
    {
        public int PageCount { get; private set; }
        public List<int> Limits { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[identity].[UserSessions]", StringComparison.Ordinal))
            {
                Assert.Contains("TOP(", command.CommandText, StringComparison.OrdinalIgnoreCase);
                PageCount++;
                var limit = Assert.Single(command.Parameters.Cast<DbParameter>(), parameter => parameter.Value is int value && value == 128);
                Limits.Add(Assert.IsType<int>(limit.Value));
            }
            return ValueTask.FromResult(result);
        }
    }
}

public sealed class SignalRBoundedAudienceTests
{
    [Fact]
    public async Task NotifierStreamsAuthorizedAudienceInBoundedGroupsWithoutDroppingRecipients()
    {
        var audience = new StreamingAudience(263);
        var clients = new BoundedHubClients(audience);
        await new SignalRMessagingRealtimeNotifier(new BoundedHubContext(clients), audience, TimeProvider.System, new FixedSessionScope())
            .NotifyConversationChangedAsync(Notification(), null, CancellationToken.None);
        Assert.Equal(new[] { 128, 128, 7 }, clients.Batches.Select(batch => batch.Length));
        Assert.Equal(new[] { 128, 256, 263 }, clients.EnumeratedAtSend);
        Assert.Equal(audience.Sessions.Select(MessagingRealtimeGroups.Session).Order(), clients.Batches.SelectMany(batch => batch).Order());
    }

    [Fact]
    public async Task NotifierPropagatesFailureInsteadOfReportingPartialFanoutSuccess()
    {
        var audience = new StreamingAudience(263);
        var clients = new BoundedHubClients(audience, index =>
        { if (index == 2) throw new IOException("synthetic-private-hub-marker"); });
        await Assert.ThrowsAsync<IOException>(() => new SignalRMessagingRealtimeNotifier(new BoundedHubContext(clients), audience, TimeProvider.System, new FixedSessionScope())
            .NotifyConversationChangedAsync(Notification(), null, CancellationToken.None));
        Assert.Equal(2, clients.Batches.Count);
        Assert.Equal(256, audience.Enumerated);
    }

    [Fact]
    public async Task NotifierCancellationStopsBeforeAnotherAudienceBatch()
    {
        using var caller = new CancellationTokenSource();
        var audience = new StreamingAudience(263);
        var clients = new BoundedHubClients(audience, _ => caller.Cancel());
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SignalRMessagingRealtimeNotifier(new BoundedHubContext(clients), audience, TimeProvider.System, new FixedSessionScope())
                .NotifyConversationChangedAsync(Notification(), null, caller.Token));
        Assert.Equal(caller.Token, exception.CancellationToken);
        Assert.Single(clients.Batches);
        Assert.Equal(128, audience.Enumerated);
    }

    private static MessagingRealtimeNotification Notification() => new(Guid.NewGuid(), MessagingChangeType.ConversationChanged,
        Guid.NewGuid(), null, "synthetic-cursor", DateTime.UtcNow, 1);

    private sealed class FixedSessionScope : MakanApp.Application.Realtime.IRealtimeSessionScope
    {
        public Task<string?> AuthorizeConversationAsync(Guid sessionId, MessagingRealtimeNotification notification, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<string?>("synthetic-context");
        }
    }

    private sealed class StreamingAudience(int count) : IMessagingRealtimeAudienceResolver
    {
        public Guid[] Sessions { get; } = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        public int Enumerated { get; private set; }
        public async IAsyncEnumerable<Guid> GetActiveSessionIdsAsync(MessagingRealtimeNotification notification, Guid? audienceUserId,
            DateTime nowUtc, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach (var session in Sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Enumerated++;
                yield return session;
            }
        }
    }

    private sealed class BoundedHubContext(BoundedHubClients clients) : IHubContext<MessagingHub, IMessagingHubClient>
    {
        public IHubClients<IMessagingHubClient> Clients => clients;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class BoundedHubClients(StreamingAudience audience, Action<int>? sending = null) : IHubClients<IMessagingHubClient>
    {
        public List<string[]> Batches { get; } = [];
        public List<int> EnumeratedAtSend { get; } = [];
        public IMessagingHubClient Groups(IReadOnlyList<string> groupNames)
        {
            Assert.InRange(groupNames.Count, 1, SignalRMessagingRealtimeNotifier.MaximumGroupBatchSize);
            Batches.Add(groupNames.ToArray());
            return new BoundedHubClient(() => { EnumeratedAtSend.Add(audience.Enumerated); sending?.Invoke(Batches.Count); });
        }
        public IMessagingHubClient All => throw new NotSupportedException();
        public IMessagingHubClient AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IMessagingHubClient Client(string connectionId) => throw new NotSupportedException();
        public IMessagingHubClient Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IMessagingHubClient Group(string groupName) => throw new NotSupportedException();
        public IMessagingHubClient GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IMessagingHubClient User(string userId) => throw new NotSupportedException();
        public IMessagingHubClient Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class BoundedHubClient(Action sending) : IMessagingHubClient
    {
        public Task ClientInvalidated(MakanApp.Application.Realtime.RealtimeInvalidation notification) => throw new NotSupportedException();
        public Task ConversationChanged(MessagingRealtimeNotification notification) { sending(); return Task.CompletedTask; }
        public Task TypingChanged(TypingNotification notification) => throw new NotSupportedException();
        public Task PresenceChanged(PresenceResult notification) => throw new NotSupportedException();
    }
}
