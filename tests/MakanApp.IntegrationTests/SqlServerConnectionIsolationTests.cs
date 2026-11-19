using System.Data;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(WorkerRecoverySqlCollection.Name)]
public sealed class SqlServerConnectionIsolationTests(SqlServerConnectionIsolationFixture fixture)
    : IClassFixture<SqlServerConnectionIsolationFixture>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PooledSerializableSessionCannotChangeFreshContextDefaultReadIsolation(bool commit)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connectionString = TestPoolConnectionString(db);
        await using var pooled = new SqlConnection(connectionString);
        try
        {
            await pooled.OpenAsync();
            await using (var transaction = await pooled.BeginTransactionAsync(IsolationLevel.Serializable))
            {
                if (commit) await transaction.CommitAsync();
                else await transaction.RollbackAsync();
            }
            var before = await ReadSessionAsync(pooled);
            Assert.Equal(4, before.Isolation);
            await pooled.CloseAsync();

            db.Database.SetConnectionString(connectionString);
            var actual = await ReadSessionAsync(db);
            Assert.Equal(before.Spid, actual.Spid);
            Assert.Equal(2, actual.Isolation);
        }
        finally { SqlConnection.ClearPool(pooled); }
    }

    [Theory]
    [InlineData(IsolationLevel.Serializable, 4)]
    [InlineData(IsolationLevel.RepeatableRead, 3)]
    public async Task ExplicitBusinessTransactionKeepsRequestedIsolation(IsolationLevel isolation, int expected)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        db.Database.SetConnectionString(TestPoolConnectionString(db));
        await using var transaction = await db.Database.BeginTransactionAsync(isolation);
        Assert.Equal(expected, (await ReadSessionAsync(db)).Isolation);
        await transaction.RollbackAsync();
        SqlConnection.ClearPool((SqlConnection)db.Database.GetDbConnection());
    }

    [Fact]
    public async Task SynchronousConnectionOpenAlsoClearsPreviousSerializableIsolation()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connectionString = TestPoolConnectionString(db);
        await using var pooled = new SqlConnection(connectionString);
        try
        {
            await pooled.OpenAsync();
            await using (var transaction = await pooled.BeginTransactionAsync(IsolationLevel.Serializable)) await transaction.CommitAsync();
            await pooled.CloseAsync();
            db.Database.SetConnectionString(connectionString);
            db.Database.OpenConnection();
            Assert.Equal(2, (await ReadSessionAsync(db)).Isolation);
        }
        finally { SqlConnection.ClearPool(pooled); }
    }

    [Fact]
    public async Task AmbientSerializableTransactionIsNeverDowngraded()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        db.Database.SetConnectionString(TestPoolConnectionString(db));
        using (var ambient = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Required,
            new System.Transactions.TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.Serializable },
            System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
        {
            Assert.Equal(4, (await ReadSessionAsync(db)).Isolation);
            ambient.Complete();
        }
        SqlConnection.ClearPool((SqlConnection)db.Database.GetDbConnection());
    }

    [Fact]
    public async Task DefaultRealtimeReadReleasesConversationLockBeforeWaitingForParticipant()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var now = DateTime.UtcNow;
        var user = MakanApp.Domain.Identity.User.Create(now);
        var conversation = MakanApp.Domain.Messaging.Conversation.CreateSaved(user.Id, now);
        var participant = MakanApp.Domain.Messaging.ConversationParticipant.CreateActive(conversation.Id, user.Id, now);
        db.AddRange(user, conversation, participant); await db.SaveChangesAsync();
        var connectionString = TestPoolConnectionString(db);
        await using var pooled = new SqlConnection(connectionString);
        await pooled.OpenAsync();
        await using (var earlier = await pooled.BeginTransactionAsync(IsolationLevel.Serializable)) await earlier.CommitAsync();
        await pooled.CloseAsync();
        db.Database.SetConnectionString(connectionString);
        await db.Database.OpenConnectionAsync();
        var session = await ReadSessionAsync(db);
        await using var writer = new SqlConnection(new SqlConnectionStringBuilder(fixture.ConnectionString)
            { ApplicationName = "MakanApp_IsolationWriter_" + Guid.NewGuid().ToString("N") }.ConnectionString);
        await using var monitor = new SqlConnection(fixture.ConnectionString);
        await writer.OpenAsync(); await monitor.OpenAsync();
        await using var transaction = (SqlTransaction)await writer.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await using var child = writer.CreateCommand(); child.Transaction = transaction;
        child.CommandText = "UPDATE [messaging].[ConversationParticipants] SET [LastReadMessageSequence]=[LastReadMessageSequence] WHERE [Id]=@id";
        child.Parameters.AddWithValue("@id", participant.Id); Assert.Equal(1, await child.ExecuteNonQueryAsync());
        await using var read = db.Database.GetDbConnection().CreateCommand();
        // ترتیب join عمداً ثابت است تا همان چرخهٔ قفل Conversations -> Participants به SQL plan تصادفی وابسته نباشد.
        read.CommandText = "SELECT c.[NextChangeSequence] FROM [messaging].[Conversations] c WITH (INDEX([PK_Conversations]), FORCESEEK) INNER LOOP JOIN [messaging].[ConversationParticipants] p WITH (INDEX([PK_ConversationParticipants]), FORCESEEK) ON p.[ConversationId]=c.[Id] WHERE c.[Id]=@conversation AND p.[Id]=@participant OPTION (FORCE ORDER, MAXDOP 1)";
        var conversationParameter = read.CreateParameter(); conversationParameter.ParameterName = "@conversation"; conversationParameter.Value = conversation.Id; read.Parameters.Add(conversationParameter);
        var participantParameter = read.CreateParameter(); participantParameter.ParameterName = "@participant"; participantParameter.Value = participant.Id; read.Parameters.Add(participantParameter);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var reading = read.ExecuteScalarAsync(budget.Token);
        var committed = false;
        try
        {
            await using var waiting = monitor.CreateCommand();
            waiting.CommandText = "SELECT wait_type FROM sys.dm_exec_requests WHERE session_id=@session";
            waiting.Parameters.AddWithValue("@session", session.Spid);
            while (!reading.IsCompleted)
            {
                budget.Token.ThrowIfCancellationRequested();
                if (await waiting.ExecuteScalarAsync(budget.Token) is string wait && wait.StartsWith("LCK_M_", StringComparison.Ordinal)) break;
                await Task.Delay(10, budget.Token);
            }
            await using var parent = writer.CreateCommand(); parent.Transaction = transaction;
            parent.CommandText = "SET LOCK_TIMEOUT 1000; UPDATE [messaging].[Conversations] SET [NextChangeSequence]=[NextChangeSequence] WHERE [Id]=@id";
            parent.Parameters.AddWithValue("@id", conversation.Id);
            Assert.Equal(1, await parent.ExecuteNonQueryAsync(budget.Token));
            await transaction.CommitAsync(budget.Token); committed = true;
            Assert.Equal(1L, Convert.ToInt64(await reading));
        }
        finally
        {
            if (!committed) await transaction.RollbackAsync();
            await reading.WaitAsync(TimeSpan.FromSeconds(5));
            SqlConnection.ClearPool(pooled);
            SqlConnection.ClearPool(writer);
        }
    }

    [Fact]
    public async Task CommittedMessageStillReachesSignalRWithTaintedPoolAndConcurrentParticipantWrite()
    {
        var control = fixture.Factory.Services.GetRequiredService<RealtimeFailureSwitch>();
        await using var writer = new SqlConnection(new SqlConnectionStringBuilder(fixture.ConnectionString)
            { ApplicationName = "MakanApp_LiveIsolationWriter_" + Guid.NewGuid().ToString("N") }.ConnectionString);
        await writer.OpenAsync();
        SqlTransaction? transaction = null;
        var injected = 0;
        control.BeforePublication = async (notification, cancellationToken) =>
        {
            if (notification.Type != MakanApp.Domain.Messaging.MessagingChangeType.MessageCreated ||
                Interlocked.Exchange(ref injected, 1) != 0) return;
            Guid participantId;
            await using (var scope = fixture.Factory.Services.CreateAsyncScope())
                participantId = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().ConversationParticipants
                    .Where(p => p.ConversationId == notification.ConversationId).Select(p => p.Id).FirstAsync(cancellationToken);
            transaction = (SqlTransaction)await writer.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            await using var update = writer.CreateCommand(); update.Transaction = transaction;
            update.CommandText = "UPDATE [messaging].[ConversationParticipants] SET [LastReadMessageSequence]=[LastReadMessageSequence] WHERE [Id]=@id";
            update.Parameters.AddWithValue("@id", participantId);
            Assert.Equal(1, await update.ExecuteNonQueryAsync(cancellationToken));
            // فقط pool خصوصیِ تک‌اتصالی این fixture آلوده می‌شود؛ worker در همین مرز متوقف است و تراکنش دیگری مصرفش نمی‌کند.
            await using var pooled = new SqlConnection(fixture.ConnectionString);
            await pooled.OpenAsync(cancellationToken);
            await using var earlier = await pooled.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            await earlier.CommitAsync(cancellationToken);
        };
        try
        {
            // همان تست واقعی HTTP -> SQL -> SignalR است؛ مهلت ده‌ثانیه‌ای آن نیز بدون تغییر باقی می‌ماند.
            await new MessagingEndpointsTests(fixture.Factory).AuthorizedParticipantReceivesInvalidationAndRecoversDurablyFromDelta();
            Assert.Equal(1, injected);
        }
        finally
        {
            control.Reset();
            if (transaction is not null) { await transaction.RollbackAsync(); await transaction.DisposeAsync(); }
            SqlConnection.ClearPool(writer);
        }
    }

    private static string TestPoolConnectionString(MakanDbContext db)
    {
        var builder = new SqlConnectionStringBuilder(db.Database.GetConnectionString())
        {
            ApplicationName = "MakanApp_RealtimeIsolation_" + Guid.NewGuid().ToString("N")
        };
        Assert.StartsWith("(localdb)", builder.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(MakanAppWebApplicationFactory.KnowledgeProbeDatabasePrefix, builder.InitialCatalog, StringComparison.Ordinal);
        return builder.ConnectionString;
    }

    private static Task<SessionState> ReadSessionAsync(MakanDbContext db) => db.Database.SqlQueryRaw<SessionState>(
        "SELECT CONVERT(int, @@SPID) AS Spid, CONVERT(int, transaction_isolation_level) AS Isolation FROM sys.dm_exec_sessions WHERE session_id=@@SPID")
        .SingleAsync();

    private static async Task<SessionState> ReadSessionAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT @@SPID, transaction_isolation_level FROM sys.dm_exec_sessions WHERE session_id=@@SPID";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new() { Spid = Convert.ToInt32(reader[0]), Isolation = Convert.ToInt32(reader[1]) };
    }

    public sealed class SessionState
    {
        public int Spid { get; set; }
        public int Isolation { get; set; }
    }
}

// همین مجموعهٔ غیرموازیِ بازیابی، از تداخل متغیر محیطیِ میزبان‌های تست جلوگیری می‌کند؛ DB هر fixture مستقل است.
public sealed class SqlServerConnectionIsolationFixture : IAsyncLifetime
{
    public MakanAppWebApplicationFactory Factory { get; } = MakanAppWebApplicationFactory.CreateRealtimeIsolationProbe();
    public string ConnectionString
    {
        get
        {
            using var scope = Factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString()!;
        }
    }
    public Task InitializeAsync() => ((IAsyncLifetime)Factory).InitializeAsync();
    public Task DisposeAsync() => ((IAsyncLifetime)Factory).DisposeAsync();
}
