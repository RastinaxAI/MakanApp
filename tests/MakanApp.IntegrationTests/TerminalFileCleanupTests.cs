using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class TerminalFileCleanupTests
{
    [Fact]
    public async Task DisabledCleanupDoesNotQuerySqlOrDeleteAnyBytes()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var file = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
        var commands = new CountingCommands();
        await using var db = isolated.CreateDb(commands);
        using var cursor = new TerminalFileCleanupCursor();
        var result = await isolated.Processor(db, new TerminalFileCleanupOptions(), cursor).ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(new TerminalFileCleanupBatchResult(0, 0, 0, 0, 0), result);
        Assert.Equal(0, commands.Count);
        Assert.True(await isolated.Storage.ExistsAsync(file.StorageKey, CancellationToken.None));
    }

    [Fact]
    public async Task OnlyOldTerminalUnreferencedBytesAreDeletedAndAllSqlMetadataRemainsUnchanged()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var deleted = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
        var rejected = await isolated.SeedFileAsync(FileAssetStatus.Rejected);
        var ready = await isolated.SeedFileAsync(FileAssetStatus.Ready);
        var pending = await isolated.SeedFileAsync(FileAssetStatus.Pending);
        var retained = await isolated.SeedFileAsync(FileAssetStatus.Ready, retained: true);
        var recent = await isolated.SeedFileAsync(FileAssetStatus.Deleted, terminalAt: isolated.Now.AddMinutes(-30));
        var before = await isolated.MetadataAsync();
        using var cursor = new TerminalFileCleanupCursor();
        var first = await isolated.BatchAsync(cursor);
        var second = await isolated.BatchAsync(cursor);
        Assert.Equal(2, first.Deleted + second.Deleted);
        Assert.Equal(1, first.Skipped + second.Skipped);
        Assert.False(await isolated.Storage.ExistsAsync(deleted.StorageKey, CancellationToken.None));
        Assert.False(await isolated.Storage.ExistsAsync(rejected.StorageKey, CancellationToken.None));
        foreach (var file in new[] { ready, pending, retained, recent })
            Assert.True(await isolated.Storage.ExistsAsync(file.StorageKey, CancellationToken.None));
        await isolated.AssertMetadataUnchangedAsync(before);

        // سقف فنی grace نباید در محاسبه cutoff زیر DateTime.MinValue برود.
        using var veryLongCursor = new TerminalFileCleanupCursor();
        var veryLong = new TerminalFileCleanupOptions { Enabled = true, GracePeriodHours = TerminalFileCleanupOptions.MaximumGraceHours };
        Assert.Equal(0, (await isolated.BatchAsync(veryLongCursor, veryLong)).Examined);
        Assert.Equal(0, (await isolated.BatchAsync(veryLongCursor, veryLong)).Examined);
    }

    [Fact]
    public async Task AllAttachmentFamiliesAndEveryScheduledAndReportHistoryStateAreExcluded()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var message = await isolated.SeedMessageAsync();
        var protectedFiles = new List<FileAsset>();
        await using (var db = isolated.CreateDb())
        {
            var file = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
            protectedFiles.Add(file);
            db.MessageAttachments.Add(MessageAttachment.Create(message.Id, file.Id, MessageKind.File, isolated.Now));
            var evidence = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
            protectedFiles.Add(evidence);
            var report = AbuseReport.Create(isolated.UserId, isolated.OtherUserId, message.ConversationId, message.Id,
                Guid.NewGuid(), AbuseReportReason.Other, null, MessageKind.Text, "synthetic evidence", new byte[8], new byte[32], isolated.Now);
            var moderation = ModerationCase.CreateForReport(message.ConversationId, report.Id,
                ModerationQueueScope.PlatformSafety, ModerationPriority.Normal, isolated.Now);
            db.AddRange(report, moderation, ModerationEvidenceAttachment.Create(moderation.Id, evidence.Id, isolated.Now));

            foreach (var state in Enum.GetValues<ScheduledMessageStatus>())
            {
                var scheduledFile = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
                protectedFiles.Add(scheduledFile);
                var scheduled = ScheduledMessage.Create(message.ConversationId, isolated.UserId, Guid.NewGuid(),
                    new(WorkspaceType.Personal, null, null, null, null),
                    new(MessageKind.File, null, [scheduledFile.Id], null, [], null, null, false),
                    new byte[32], isolated.Now.AddHours(-3), isolated.Now.AddHours(-4));
                if (state == ScheduledMessageStatus.Cancelled) scheduled.Cancel(isolated.Now.AddHours(-2));
                if (state is ScheduledMessageStatus.Failed or ScheduledMessageStatus.Dispatched)
                {
                    var token = Guid.NewGuid();
                    scheduled.Claim(token, isolated.Now.AddHours(-2), isolated.Now.AddHours(-1));
                    if (state == ScheduledMessageStatus.Failed) scheduled.Fail(token, "SYNTHETIC_FAILURE", false, isolated.Now.AddHours(-2));
                    else scheduled.Dispatch(token, message.Id, isolated.Now.AddHours(-2));
                }
                db.ScheduledMessages.Add(scheduled);
            }

            foreach (var state in Enum.GetValues<ReportExportJobStatus>())
            {
                var job = isolated.NewReportJob();
                var canonical = await isolated.SeedFileAsync(FileAssetStatus.Deleted, id: job.Id);
                protectedFiles.Add(canonical);
                if (state != ReportExportJobStatus.Queued) job.Claim(isolated.Now, isolated.Now.AddMinutes(2));
                if (state == ReportExportJobStatus.Completed) job.Complete(canonical.Id, isolated.Now);
                if (state == ReportExportJobStatus.Failed) job.Fail(ReportExportErrorCodes.GenerationFailed, isolated.Now);
                db.ReportExportJobs.Add(job);
            }
            var outputOnly = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
            protectedFiles.Add(outputOnly);
            var outputJob = isolated.NewReportJob();
            outputJob.Claim(isolated.Now, isolated.Now.AddMinutes(2));
            outputJob.Complete(outputOnly.Id, isolated.Now);
            db.ReportExportJobs.Add(outputJob);
            await db.SaveChangesAsync();
        }

        var academic = await isolated.Factory.CreateAcademicClassAsync(isolated.OrganizationId);
        var person = (await isolated.Factory.CreateOrganizationPersonsAsync(isolated.OrganizationId, 1))[0];
        var enrollment = await isolated.Factory.CreateEnrollmentAsync(isolated.OrganizationId, academic.ClassId, person);
        var assignment = await isolated.Factory.CreateDashboardAssignmentAsync(isolated.OrganizationId, academic.ClassId, enrollment,
            isolated.MembershipId, isolated.Now.AddDays(1), "PH6 synthetic assignment", DashboardAssignmentSeedState.Submitted);
        var submissionFile = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
        protectedFiles.Add(submissionFile);
        await using (var db = isolated.CreateDb())
        {
            db.SubmissionAttachments.Add(SubmissionAttachment.Create(isolated.OrganizationId, assignment.SubmissionAttemptId!.Value,
                submissionFile.Id, isolated.Now));
            await db.SaveChangesAsync();
        }
        var ordinary = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
        var before = await isolated.MetadataAsync();
        using var cursor = new TerminalFileCleanupCursor();
        _ = await isolated.BatchAsync(cursor);
        var result = await isolated.BatchAsync(cursor);
        Assert.Equal(protectedFiles.Count, result.Skipped);
        Assert.Equal(1, result.Deleted);
        foreach (var file in protectedFiles) Assert.True(await isolated.Storage.ExistsAsync(file.StorageKey, CancellationToken.None));
        Assert.False(await isolated.Storage.ExistsAsync(ordinary.StorageKey, CancellationToken.None));
        await isolated.AssertMetadataUnchangedAsync(before);
    }

    [Fact]
    public async Task IndependentStatusCursorsSurviveNewScopesAndCannotStarveDeletedBehindRejectedBacklog()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var created = isolated.Now.AddDays(-5);
        var skipped = await isolated.SeedFileAsync(FileAssetStatus.Rejected, createdAt: created, terminalAt: isolated.Now.AddMinutes(-1));
        var missing = await isolated.SeedFileAsync(FileAssetStatus.Rejected, createdAt: created.AddMinutes(1), writeBytes: false);
        var eligible = await isolated.SeedFileAsync(FileAssetStatus.Rejected, createdAt: created.AddMinutes(2));
        var deleted = await isolated.SeedFileAsync(FileAssetStatus.Deleted);
        using var cursor = new TerminalFileCleanupCursor();
        var options = new TerminalFileCleanupOptions { Enabled = true, GracePeriodHours = 1, BatchSize = 1 };
        Assert.Equal(1, (await isolated.BatchAsync(cursor, options)).Skipped);
        Assert.Equal(1, (await isolated.BatchAsync(cursor, options)).Deleted);
        Assert.False(await isolated.Storage.ExistsAsync(deleted.StorageKey, CancellationToken.None));
        Assert.Equal(1, (await isolated.BatchAsync(cursor, options)).AlreadyMissing);
        _ = await isolated.BatchAsync(cursor, options);
        Assert.Equal(1, (await isolated.BatchAsync(cursor, options)).Deleted);
        Assert.False(await isolated.Storage.ExistsAsync(eligible.StorageKey, CancellationToken.None));
        Assert.True(await isolated.Storage.ExistsAsync(skipped.StorageKey, CancellationToken.None));
        await using var verify = isolated.CreateDb();
        Assert.Equal(4, await verify.FileAssets.CountAsync());
        Assert.NotNull(await verify.FileAssets.SingleAsync(file => file.Id == missing.Id));
    }

    [Fact]
    public async Task IoFailureIsSafelyMeasuredAndDoesNotBlockTheFollowingCandidate()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var failure = await isolated.SeedFileAsync(FileAssetStatus.Rejected, createdAt: isolated.Now.AddDays(-5));
        Assert.True(await isolated.Storage.DeleteTerminalBlobAsync(failure.StorageKey, CancellationToken.None));
        Directory.CreateDirectory(Path.Combine(isolated.Factory.StorageRootPath, failure.StorageKey.Replace('/', Path.DirectorySeparatorChar)));
        var success = await isolated.SeedFileAsync(FileAssetStatus.Rejected, createdAt: isolated.Now.AddDays(-4));
        var before = await isolated.MetadataAsync();
        var logger = new CapturingLogger();
        var readings = new Dictionary<string, long>();
        var meter = isolated.Factory.Services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName);
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, target) =>
        { if (ReferenceEquals(instrument.Meter, meter) && instrument.Name == "makan.file_cleanup.results") target.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var tag = Assert.Single(tags.ToArray());
            Assert.Equal("outcome", tag.Key);
            var name = Assert.IsType<string>(tag.Value);
            Assert.Contains(name, new[] { "Examined", "Deleted", "AlreadyMissing", "Skipped", "Failed", "Other" });
            readings[name] = readings.GetValueOrDefault(name) + value;
        });
        listener.Start();
        using var cursor = new TerminalFileCleanupCursor();
        await using var db = isolated.CreateDb();
        var result = await isolated.Processor(db, CleanupScope.EnabledOptions, cursor, logger).ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(new TerminalFileCleanupBatchResult(2, 1, 0, 0, 1), result);
        Assert.Equal(2, readings["Examined"]);
        Assert.Equal(1, readings["Deleted"]);
        Assert.Equal(1, readings["Failed"]);
        Assert.Null(Assert.Single(logger.Exceptions));
        var log = Assert.Single(logger.Messages);
        Assert.Contains("TERMINAL_FILE_CLEANUP_FAILED", log);
        Assert.DoesNotContain(failure.StorageKey, log);
        Assert.DoesNotContain(isolated.Factory.StorageRootPath, log);
        Assert.False(await isolated.Storage.ExistsAsync(success.StorageKey, CancellationToken.None));
        await isolated.AssertMetadataUnchangedAsync(before);
    }

    [Fact]
    public async Task PendingForeignKeyReferenceWinsTheRaceAndIsObservedBeforePhysicalDeletion()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var file = await isolated.SeedFileAsync(FileAssetStatus.Rejected);
        var message = await isolated.SeedMessageAsync();
        await using var writer = isolated.CreateDb();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        writer.MessageAttachments.Add(MessageAttachment.Create(message.Id, file.Id, MessageKind.File, isolated.Now));
        await writer.SaveChangesAsync();
        var connection = (SqlConnection)writer.Database.GetDbConnection();
        using var cursor = new TerminalFileCleanupCursor();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var processing = isolated.BatchAsync(cursor, cancellationToken: cancellation.Token);
        try
        {
            Assert.True(await WaitForBlockedSqlAsync(connection, processing));
            await transaction.CommitAsync();
            var result = await processing.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, result.Skipped);
            Assert.Equal(0, result.Deleted);
            Assert.True(await isolated.Storage.ExistsAsync(file.StorageKey, CancellationToken.None));
            await using var verify = isolated.CreateDb();
            Assert.Equal(1, await verify.MessageAttachments.CountAsync(item => item.FileAssetId == file.Id));
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                if (transaction.GetDbTransaction().Connection is not null) await transaction.RollbackAsync();
            }
            finally { _ = await Record.ExceptionAsync(() => processing.WaitAsync(TimeSpan.FromSeconds(5))); }
        }
    }

    [Fact]
    public async Task CancellationOfBlockedSqlLeavesBytesAndMetadataRecoverable()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var file = await isolated.SeedFileAsync(FileAssetStatus.Rejected);
        var before = await isolated.MetadataAsync();
        await using var blocker = isolated.CreateDb();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        _ = await blocker.FileAssets.FromSqlInterpolated($"SELECT * FROM [storage].[FileAssets] WITH (XLOCK,HOLDLOCK) WHERE [Id]={file.Id}")
            .AsNoTracking().SingleAsync();
        var connection = (SqlConnection)blocker.Database.GetDbConnection();
        using var cursor = new TerminalFileCleanupCursor();
        using var cancellation = new CancellationTokenSource();
        var processing = isolated.BatchAsync(cursor, cancellationToken: cancellation.Token);
        try
        {
            Assert.True(await WaitForBlockedSqlAsync(connection, processing));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            cancellation.Cancel();
            try { await transaction.RollbackAsync(); }
            finally { _ = await Record.ExceptionAsync(() => processing.WaitAsync(TimeSpan.FromSeconds(5))); }
        }
        Assert.True(await isolated.Storage.ExistsAsync(file.StorageKey, CancellationToken.None));
        await isolated.AssertMetadataUnchangedAsync(before);
        using var recoveredCursor = new TerminalFileCleanupCursor();
        Assert.Equal(1, (await isolated.BatchAsync(recoveredCursor)).Deleted);
    }

    [Fact]
    public async Task ConcurrentIndependentProcessorsAreIdempotentWithoutDeletingMetadata()
    {
        await using var isolated = await CleanupScope.CreateAsync();
        var file = await isolated.SeedFileAsync(FileAssetStatus.Rejected);
        var before = await isolated.MetadataAsync();
        using var firstCursor = new TerminalFileCleanupCursor();
        using var secondCursor = new TerminalFileCleanupCursor();
        var results = await Task.WhenAll(isolated.BatchAsync(firstCursor), isolated.BatchAsync(secondCursor));
        Assert.Equal(1, results.Sum(item => item.Deleted));
        Assert.Equal(1, results.Sum(item => item.AlreadyMissing));
        Assert.Equal(0, results.Sum(item => item.Failed));
        Assert.False(await isolated.Storage.ExistsAsync(file.StorageKey, CancellationToken.None));
        await isolated.AssertMetadataUnchangedAsync(before);
    }

    private static async Task<bool> WaitForBlockedSqlAsync(SqlConnection blocker, Task processing)
    {
        var settings = new SqlConnectionStringBuilder(blocker.ConnectionString) { Pooling = false, ConnectTimeout = 5 };
        Assert.StartsWith("MakanApp_Recovery_PH6_", settings.InitialCatalog, StringComparison.Ordinal);
        await using var observer = new SqlConnection(settings.ConnectionString);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await observer.OpenAsync(timeout.Token);
        var started = Stopwatch.GetTimestamp();
        while (!processing.IsCompleted && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5))
        {
            await using var command = observer.CreateCommand();
            command.CommandTimeout = 1;
            command.CommandText = "SELECT COUNT_BIG(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID() AND blocking_session_id=@blocker AND wait_type LIKE N'LCK_M_%'";
            command.Parameters.AddWithValue("@blocker", blocker.ServerProcessId);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! > 0) return true;
            await Task.Delay(20, timeout.Token);
        }
        return false;
    }

    private sealed class CountingCommands : DbCommandInterceptor
    {
        public int Count { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Count++; return ValueTask.FromResult(result); }
    }

    private sealed class CapturingLogger : ILogger<TerminalFileCleanupProcessor>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Messages.Add(formatter(state, exception)); Exceptions.Add(exception); }
    }

    private sealed class CleanupScope(MakanAppWebApplicationFactory factory, string connectionString, Guid userId, Guid otherUserId,
        Guid organizationId, Guid membershipId) : IAsyncDisposable
    {
        public static TerminalFileCleanupOptions EnabledOptions => new() { Enabled = true, GracePeriodHours = 1 };
        public MakanAppWebApplicationFactory Factory => factory;
        public LocalFileStorage Storage => factory.Services.GetRequiredService<LocalFileStorage>();
        public Guid UserId => userId;
        public Guid OtherUserId => otherUserId;
        public Guid OrganizationId => organizationId;
        public Guid MembershipId => membershipId;
        public DateTime Now => factory.Clock.GetUtcNow().UtcDateTime;
        public MakanDbContext CreateDb(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(interceptors).Options);
        public TerminalFileCleanupProcessor Processor(MakanDbContext db, TerminalFileCleanupOptions options, TerminalFileCleanupCursor cursor,
            ILogger<TerminalFileCleanupProcessor>? logger = null) => new(db, Storage, options, cursor, factory.Clock,
            factory.Services.GetRequiredService<WorkerTelemetry>(), logger ?? NullLogger<TerminalFileCleanupProcessor>.Instance);
        public async Task<TerminalFileCleanupBatchResult> BatchAsync(TerminalFileCleanupCursor cursor, TerminalFileCleanupOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await using var db = CreateDb();
            return await Processor(db, options ?? EnabledOptions, cursor).ProcessBatchAsync(cancellationToken);
        }

        public async Task<FileAsset> SeedFileAsync(FileAssetStatus status, DateTime? createdAt = null, DateTime? terminalAt = null,
            bool retained = false, bool writeBytes = true, Guid? id = null)
        {
            var created = createdAt ?? Now.AddDays(-3);
            var fileId = id ?? Guid.NewGuid();
            var key = FileUploadPolicy.CreateStorageKey(fileId, created);
            var file = FileAsset.CreatePending(fileId, organizationId, userId, "synthetic.txt", key, "text/plain", created, created.AddDays(1));
            StoredFileResult? stored = null;
            if (writeBytes)
            {
                using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("PH6 synthetic bytes"));
                stored = await Storage.SaveAsync(key, bytes, 1024, CancellationToken.None);
            }
            if (status != FileAssetStatus.Pending)
                file.MarkReady(stored?.SizeBytes ?? 1, stored?.Sha256Hash ?? new string('A', 64), created);
            if (retained) file.Retain(created);
            if (status == FileAssetStatus.Deleted) file.MarkDeleted(terminalAt ?? Now.AddHours(-2));
            if (status == FileAssetStatus.Rejected) file.MarkRejected(terminalAt ?? Now.AddHours(-2));
            await using var db = CreateDb();
            db.FileAssets.Add(file);
            await db.SaveChangesAsync();
            return file;
        }

        public async Task<Message> SeedMessageAsync()
        {
            await using var db = CreateDb();
            var conversation = Conversation.CreateSaved(userId, Now);
            var participant = ConversationParticipant.CreateActive(conversation.Id, userId, Now, ConversationParticipantRole.Owner);
            var message = Message.CreateText(conversation.Id, participant.Id, userId, Guid.NewGuid(), conversation.AllocateNextMessageSequence(),
                "PH6 synthetic message", Message.StorageMaximumTextLength, Now);
            db.AddRange(conversation, participant, message);
            await db.SaveChangesAsync();
            return message;
        }

        public ReportExportJob NewReportJob() => ReportExportJob.Create(organizationId, userId, Guid.NewGuid(), membershipId,
            OrganizationRole.Manager, null, ReportExportType.ManagerAttendance, ReportExportFormat.Csv, Now, null, null, null, null,
            null, null, null, "classtitleasc", 0, Now);

        public async Task<FileAsset[]> MetadataAsync()
        {
            await using var db = CreateDb();
            return await db.FileAssets.AsNoTracking().ToArrayAsync();
        }

        public async Task AssertMetadataUnchangedAsync(FileAsset[] before)
        {
            var after = await MetadataAsync();
            Assert.Equal(before.Length, after.Length);
            foreach (var old in before)
            {
                var current = Assert.Single(after, file => file.Id == old.Id);
                Assert.Equal(old.Status, current.Status);
                Assert.Equal(old.StorageKey, current.StorageKey);
                Assert.Equal(old.RowVersion, current.RowVersion);
                Assert.Equal(old.SizeBytes, current.SizeBytes);
                Assert.Equal(old.DeletedAtUtc, current.DeletedAtUtc);
                Assert.Equal(old.RejectedAtUtc, current.RejectedAtUtc);
                Assert.Equal(old.RetainedAtUtc, current.RetainedAtUtc);
            }
        }

        public static async Task<CleanupScope> CreateAsync()
        {
            var id = Guid.NewGuid().ToString("N");
            var factory = new MakanAppWebApplicationFactory("MakanApp_Recovery_PH6_" + id,
                Path.Combine(Path.GetTempPath(), "MakanApp-StorageTests", "PH6-" + id),
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            try
            {
                await ((IAsyncLifetime)factory).InitializeAsync();
                factory.Clock.SetUtcNow(DateTime.UtcNow.AddDays(1));
                await using var scope = factory.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                var now = factory.Clock.GetUtcNow().UtcDateTime;
                var user = User.Create(now);
                var other = User.Create(now);
                var organization = MakanApp.Domain.Organization.Organization.Create("PH6 synthetic cleanup", now);
                var membership = Membership.CreateActive(user.Id, organization.Id, now);
                db.AddRange(user, other, organization, membership);
                await db.SaveChangesAsync();
                return new(factory, db.Database.GetConnectionString()!, user.Id, other.Id, organization.Id, membership.Id);
            }
            catch
            {
                await ((IAsyncLifetime)factory).DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() => await ((IAsyncLifetime)factory).DisposeAsync();
    }
}
