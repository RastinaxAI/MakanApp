using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Reports;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Reports.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ReportExportRecoveryTests
{
    [Fact]
    public async Task PausedOldFileWriteCannotOverwriteOrDeleteTheNewClaimantsPublishedOutput()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using var oldDb = isolated.CreateDb();
        var pausedStorage = new PausedStorage(isolated.Storage);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var oldProcessing = isolated.Processor(oldDb, pausedStorage, new TextExporter("old attempt")).ProcessNextAsync(cancellation.Token);
        try
        {
            await pausedStorage.Written.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var oldCandidate = Assert.Single(pausedStorage.SavedKeys);
            Assert.True(await isolated.Storage.ExistsAsync(oldCandidate, CancellationToken.None));
            isolated.AdvancePastLease();
            await using (var newDb = isolated.CreateDb())
                Assert.True(await isolated.Processor(newDb, isolated.Storage, new TextExporter("new attempt")).ProcessNextAsync(CancellationToken.None));

            var published = await isolated.ReadOutputAsync(jobId);
            Assert.Equal("new attempt", published.Content);
            Assert.NotEqual(oldCandidate, published.File.StorageKey);
            pausedStorage.Resume.TrySetResult();
            Assert.True(await oldProcessing.WaitAsync(TimeSpan.FromSeconds(10)));

            var afterOldResumed = await isolated.ReadOutputAsync(jobId);
            Assert.Equal(ReportExportJobStatus.Completed, afterOldResumed.Job.Status);
            Assert.Equal(2, afterOldResumed.Job.AttemptCount);
            Assert.Equal(jobId, afterOldResumed.Job.OutputFileAssetId);
            Assert.Equal(published.File.StorageKey, afterOldResumed.File.StorageKey);
            Assert.Equal("new attempt", afterOldResumed.Content);
            Assert.DoesNotContain(published.File.StorageKey, pausedStorage.DeletedKeys);
            Assert.False(await isolated.Storage.ExistsAsync(oldCandidate, CancellationToken.None));
            await using var verification = isolated.CreateDb();
            Assert.Equal(1, await verification.FileAssets.CountAsync());
        }
        finally
        {
            cancellation.Cancel();
            pausedStorage.Resume.TrySetResult();
            _ = await Record.ExceptionAsync(() => oldProcessing.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public async Task ReadyPublicationAfterCrashIsReusedWithoutRegenerationOrHistoricalByteMutation()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using (var originalDb = isolated.CreateDb())
        {
            var job = await isolated.ClaimAsync(originalDb);
            await using var generated = TextFile("published before crash");
            Assert.Equal(jobId, await isolated.Output(originalDb, isolated.Storage).SaveAsync(
                job, job.RowVersion.ToArray(), generated, 1024, CancellationToken.None));
        }

        var beforeRecovery = await isolated.ReadOutputAsync(jobId);
        Assert.Equal(ReportExportJobStatus.Processing, beforeRecovery.Job.Status);
        isolated.AdvancePastLease();
        var forbiddenExporter = new TextExporter("must not generate", fail: true);
        await using (var recoveredDb = isolated.CreateDb())
            Assert.True(await isolated.Processor(recoveredDb, isolated.Storage, forbiddenExporter).ProcessNextAsync(CancellationToken.None));
        var recovered = await isolated.ReadOutputAsync(jobId);
        Assert.Equal(ReportExportJobStatus.Completed, recovered.Job.Status);
        Assert.Equal(jobId, recovered.Job.OutputFileAssetId);
        Assert.Equal(beforeRecovery.File.StorageKey, recovered.File.StorageKey);
        Assert.Equal("published before crash", recovered.Content);
        Assert.Equal(0, forbiddenExporter.Calls);
    }

    [Fact]
    public async Task SuccessfulPublicationWithLostCommitAcknowledgementNeverDeletesCanonicalBytes()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using var claimDb = isolated.CreateDb();
        var job = await isolated.ClaimAsync(claimDb);
        var observedStorage = new ObservedStorage(isolated.Storage);
        await using (var publicationDb = isolated.CreateDb(new CommitAcknowledgementFailure(1)))
        await using (var generated = TextFile("commit succeeded but acknowledgement was lost"))
        {
            await Assert.ThrowsAsync<IOException>(() => isolated.Output(publicationDb, observedStorage).SaveAsync(
                job, job.RowVersion.ToArray(), generated, 1024, CancellationToken.None));
        }

        var persisted = await isolated.ReadOutputAsync(jobId);
        Assert.Equal(FileAssetStatus.Ready, persisted.File.Status);
        Assert.Equal(Assert.Single(observedStorage.SavedKeys), persisted.File.StorageKey);
        Assert.Empty(observedStorage.DeletedKeys);
        Assert.Equal("commit succeeded but acknowledgement was lost", persisted.Content);
        isolated.AdvancePastLease();
        var forbiddenExporter = new TextExporter("must not generate", fail: true);
        await using (var recoveredDb = isolated.CreateDb())
            Assert.True(await isolated.Processor(recoveredDb, isolated.Storage, forbiddenExporter).ProcessNextAsync(CancellationToken.None));
        Assert.Equal(0, forbiddenExporter.Calls);
        Assert.Equal(ReportExportJobStatus.Completed, (await isolated.ReadOutputAsync(jobId)).Job.Status);
    }

    [Fact]
    public async Task CompletionAcknowledgementFailurePropagatesWithoutRetryingALocallyCompletedJob()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using (var processingDb = isolated.CreateDb(new CommitAcknowledgementFailure(3)))
        {
            // Claim، انتشار FileAsset و سپس ثبت Completed هرکدام transaction کوتاه مستقل دارند.
            await Assert.ThrowsAsync<IOException>(() => isolated.Processor(processingDb, isolated.Storage,
                new TextExporter("completed once")).ProcessNextAsync(CancellationToken.None));
        }

        var completed = await isolated.ReadOutputAsync(jobId);
        Assert.Equal(ReportExportJobStatus.Completed, completed.Job.Status);
        Assert.Equal(1, completed.Job.AttemptCount);
        Assert.Equal("completed once", completed.Content);
        await using var nextDb = isolated.CreateDb();
        Assert.False(await isolated.Processor(nextDb, isolated.Storage, new TextExporter("duplicate")).ProcessNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FailedPublicationDetachesOnlyItsFileBeforeSavingTheJobRetry()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using (var processingDb = isolated.CreateDb(new FilePublicationFailure()))
        {
            Assert.True(await isolated.Processor(processingDb, isolated.Storage, new TextExporter("unpublished candidate"))
                .ProcessNextAsync(CancellationToken.None));
            Assert.Empty(processingDb.ChangeTracker.Entries<FileAsset>());
        }
        await using var verify = isolated.CreateDb();
        Assert.Equal(ReportExportJobStatus.Queued, (await verify.ReportExportJobs.SingleAsync(job => job.Id == jobId)).Status);
        Assert.Empty(await verify.FileAssets.ToArrayAsync());
    }

    [Fact]
    public async Task GeneratedStreamDisposalFailureCannotCompleteAJobAlreadyScheduledForRetry()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using (var originalDb = isolated.CreateDb())
            Assert.True(await isolated.Processor(originalDb, isolated.Storage, new DisposeFailureExporter()).ProcessNextAsync(CancellationToken.None));
        var published = await isolated.ReadOutputAsync(jobId);
        Assert.Equal(ReportExportJobStatus.Queued, published.Job.Status);
        Assert.Equal("published before disposal failure", published.Content);
        isolated.Factory.Clock.SetUtcNow(isolated.Now.AddSeconds(11));
        var forbiddenExporter = new TextExporter("must not generate", fail: true);
        await using (var recovered = isolated.CreateDb())
            Assert.True(await isolated.Processor(recovered, isolated.Storage, forbiddenExporter).ProcessNextAsync(CancellationToken.None));
        Assert.Equal(0, forbiddenExporter.Calls);
        Assert.Equal(ReportExportJobStatus.Completed, (await isolated.ReadOutputAsync(jobId)).Job.Status);
    }

    [Fact]
    public async Task RedundantCandidateCleanupFailureIsVisibleAndDoesNotInvalidateReadyOutput()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using var claimDb = isolated.CreateDb();
        var job = await isolated.ClaimAsync(claimDb);
        var version = job.RowVersion.ToArray();
        await using (var first = TextFile("immutable canonical"))
            Assert.Equal(jobId, await isolated.Output(claimDb, isolated.Storage).SaveAsync(job, version, first, 1024, CancellationToken.None));
        var before = await isolated.ReadOutputAsync(jobId);
        var logger = new CapturingLogger();
        var storage = new DeleteFailureStorage(isolated.Storage);
        var meter = isolated.Factory.Services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName);
        long cleanupFailures = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, target) =>
        { if (ReferenceEquals(instrument.Meter, meter) && instrument.Name == "makan.worker.failures") target.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var labels = tags.ToArray();
            if (labels.Any(tag => tag.Key == "stage" && Equals(tag.Value, "Cleanup"))) Interlocked.Add(ref cleanupFailures, value);
        });
        listener.Start();
        await using (var retryDb = isolated.CreateDb())
        await using (var redundant = TextFile("redundant candidate"))
            Assert.Equal(jobId, await isolated.Output(retryDb, storage, logger).SaveAsync(job, version, redundant, 1024, CancellationToken.None));
        var after = await isolated.ReadOutputAsync(jobId);
        Assert.Equal(before.File.StorageKey, after.File.StorageKey);
        Assert.Equal("immutable canonical", after.Content);
        Assert.Equal(1, cleanupFailures);
        Assert.Null(Assert.Single(logger.Exceptions));
        var message = Assert.Single(logger.Messages);
        Assert.Contains("REPORT_EXPORT_CANDIDATE_CLEANUP_FAILED", message);
        Assert.DoesNotContain("synthetic-private-path-marker", message);
        Assert.DoesNotContain(Assert.Single(storage.SavedKeys), message);
        Assert.DoesNotContain(before.File.StorageKey, storage.DeletedKeys);
    }

    [Fact]
    public async Task CancellationLeavesTheClaimRecoverableAndNeverPublishesIncompleteOutput()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        await using var cancelledDb = isolated.CreateDb();
        var storage = new PausedStorage(isolated.Storage);
        using var cancellation = new CancellationTokenSource();
        var processing = isolated.Processor(cancelledDb, storage, new TextExporter("cancelled candidate")).ProcessNextAsync(cancellation.Token);
        try
        {
            await storage.Written.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing.WaitAsync(TimeSpan.FromSeconds(5)));
            await using (var verify = isolated.CreateDb())
            {
                Assert.Empty(await verify.FileAssets.ToArrayAsync());
                Assert.Equal(ReportExportJobStatus.Processing, (await verify.ReportExportJobs.SingleAsync()).Status);
            }

            Assert.False(await isolated.Storage.ExistsAsync(Assert.Single(storage.SavedKeys), CancellationToken.None));
            isolated.AdvancePastLease();
            await using (var recovered = isolated.CreateDb())
                Assert.True(await isolated.Processor(recovered, isolated.Storage, new TextExporter("recovered")).ProcessNextAsync(CancellationToken.None));
            Assert.Equal("recovered", (await isolated.ReadOutputAsync(jobId)).Content);
        }
        finally
        {
            cancellation.Cancel();
            storage.Resume.TrySetResult();
            _ = await Record.ExceptionAsync(() => processing.WaitAsync(TimeSpan.FromSeconds(5)));
        }
    }

    [Fact]
    public async Task ExpiredClaimCannotPublishOrPersistEvenBeforeAnotherWorkerClaimsIt()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        _ = await isolated.SeedJobsAsync(1);
        await using var expiredDb = isolated.CreateDb();
        var job = await isolated.ClaimAsync(expiredDb);
        var claimVersion = job.RowVersion.ToArray();
        isolated.AdvancePastLease();
        var storage = new ObservedStorage(isolated.Storage);
        await using var generated = TextFile("expired candidate");
        Assert.Null(await isolated.Output(expiredDb, storage).SaveAsync(job, claimVersion, generated, 1024, CancellationToken.None));
        job.Fail(ReportExportErrorCodes.GenerationFailed, isolated.Now);
        Assert.False(await new EfReportExportJobStore(expiredDb, isolated.Factory.Clock)
            .TrySaveClaimChangesAsync(job, claimVersion, CancellationToken.None));
        await using var verify = isolated.CreateDb();
        Assert.Empty(await verify.FileAssets.ToArrayAsync());
        Assert.Equal(ReportExportJobStatus.Processing, (await verify.ReportExportJobs.SingleAsync()).Status);
        Assert.False(await isolated.Storage.ExistsAsync(Assert.Single(storage.SavedKeys), CancellationToken.None));
    }

    [Fact]
    public async Task LeaseIsRecheckedAfterTheCanonicalFileLockHasBeenAcquired()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        _ = await isolated.SeedJobsAsync(1);
        await using var claimDb = isolated.CreateDb();
        var job = await isolated.ClaimAsync(claimDb);
        var interceptor = new ExpireLeaseAfterFileLock(isolated.AdvancePastLease);
        await using var publicationDb = isolated.CreateDb(interceptor);
        var storage = new ObservedStorage(isolated.Storage);
        await using var generated = TextFile("lease expires while acquiring locks");
        Assert.Null(await isolated.Output(publicationDb, storage).SaveAsync(job, job.RowVersion.ToArray(), generated, 1024, CancellationToken.None));
        Assert.Equal(1, interceptor.Calls);
        await using var verify = isolated.CreateDb();
        Assert.Empty(await verify.FileAssets.ToArrayAsync());
        Assert.False(await isolated.Storage.ExistsAsync(Assert.Single(storage.SavedKeys), CancellationToken.None));
    }

    [Fact]
    public async Task ExhaustedLeaseRepairIsBoundedAndNeverFailsAnActiveFinalAttempt()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        _ = await isolated.SeedJobsAsync(EfReportExportJobStore.MaximumRepairBatchSize + 1, exhausted: true);
        var activeId = Assert.Single(await isolated.SeedJobsAsync(1, exhausted: true, activeFinalLease: true));
        await using var claimDb = isolated.CreateDb();
        var store = new EfReportExportJobStore(claimDb, isolated.Factory.Clock);
        Assert.Null(await store.TryClaimNextAsync(isolated.Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None));
        await using (var verify = isolated.CreateDb())
        {
            Assert.Equal(EfReportExportJobStore.MaximumRepairBatchSize, await verify.ReportExportJobs.CountAsync(job => job.Status == ReportExportJobStatus.Failed));
            Assert.Equal(ReportExportJobStatus.Processing, (await verify.ReportExportJobs.SingleAsync(job => job.Id == activeId)).Status);
        }

        Assert.Null(await store.TryClaimNextAsync(isolated.Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None));
        await using (var verify = isolated.CreateDb())
            Assert.Equal(EfReportExportJobStore.MaximumRepairBatchSize + 1, await verify.ReportExportJobs.CountAsync(job => job.Status == ReportExportJobStatus.Failed));
        isolated.AdvancePastLease();
        Assert.Null(await store.TryClaimNextAsync(isolated.Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None));
        await using var final = isolated.CreateDb();
        Assert.Equal(ReportExportJobStatus.Failed, (await final.ReportExportJobs.SingleAsync(job => job.Id == activeId)).Status);
    }

    private static GeneratedReportFile TextFile(string text) => new(new MemoryStream(Encoding.UTF8.GetBytes(text)), "text/csv", "report.csv");

    private sealed class TextExporter(string text, bool fail = false) : IReportExporter
    {
        public int Calls { get; private set; }
        public Task<GeneratedReportFile> GenerateAsync(ReportExportJob job, CancellationToken cancellationToken)
        {
            Calls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (fail) throw new InvalidOperationException("تولید دوباره خروجی آماده مجاز نیست.");
            return Task.FromResult(TextFile(text));
        }
    }

    private sealed class DisposeFailureExporter : IReportExporter
    {
        public Task<GeneratedReportFile> GenerateAsync(ReportExportJob job, CancellationToken cancellationToken) => Task.FromResult(
            new GeneratedReportFile(new DisposeFailureStream(), "text/csv", "report.csv"));
    }

    private sealed class DisposeFailureStream() : MemoryStream(Encoding.UTF8.GetBytes("published before disposal failure"))
    {
        public override ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.FromException(new IOException("خطای مصنوعی Dispose خروجی گزارش."));
        }
    }

    private sealed class AllowedDataSource : IReportExportDataSource
    {
        public Task EnsureCurrentAccessAsync(Guid userId, Guid sessionId, ReportExportJob job, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ReportExportPreparation> PrepareAsync(Guid userId, Guid sessionId, ReportExportType reportType, ReportExportFilters filters, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReportExportDataPage> ReadPageAsync(ReportExportJob job, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private class ObservedStorage(IFileStorage inner) : IFileStorage
    {
        public ConcurrentBag<string> SavedKeys { get; } = [];
        public ConcurrentBag<string> DeletedKeys { get; } = [];
        public virtual async Task<StoredFileResult> SaveAsync(string storageKey, Stream content, long maxSizeBytes, CancellationToken cancellationToken)
        {
            var saved = await inner.SaveAsync(storageKey, content, maxSizeBytes, cancellationToken);
            SavedKeys.Add(storageKey);
            return saved;
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken) => inner.OpenReadAsync(storageKey, cancellationToken);
        public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken) => inner.ExistsAsync(storageKey, cancellationToken);
        public virtual Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            DeletedKeys.Add(storageKey);
            return inner.DeleteAsync(storageKey, cancellationToken);
        }
    }

    private sealed class DeleteFailureStorage(IFileStorage inner) : ObservedStorage(inner)
    {
        public override Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
        {
            DeletedKeys.Add(storageKey);
            return Task.FromException(new IOException("synthetic-private-path-marker"));
        }
    }

    private sealed class CapturingLogger : ILogger<ReportExportOutputStore>
    {
        public List<string> Messages { get; } = [];
        public List<Exception?> Exceptions { get; } = [];
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { Messages.Add(formatter(state, exception)); Exceptions.Add(exception); }
    }

    private sealed class FilePublicationFailure : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<FileAsset>().Any(entry => entry.State is EntityState.Added or EntityState.Modified))
                throw new IOException("خطای مصنوعی قبل از انتشار metadata فایل.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class PausedStorage(IFileStorage inner) : ObservedStorage(inner)
    {
        public TaskCompletionSource Written { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async Task<StoredFileResult> SaveAsync(string storageKey, Stream content, long maxSizeBytes, CancellationToken cancellationToken)
        {
            var saved = await base.SaveAsync(storageKey, content, maxSizeBytes, cancellationToken);
            Written.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
            return saved;
        }
    }

    private sealed class CommitAcknowledgementFailure(int failOnCommit) : DbTransactionInterceptor
    {
        private int _commits;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _commits) == failOnCommit)
                throw new IOException("خطای مصنوعی پاسخ Commit پس از موفقیت واقعی SQL.");
            return Task.CompletedTask;
        }
    }

    private sealed class ExpireLeaseAfterFileLock(Action expire) : DbCommandInterceptor
    {
        public int Calls { get; private set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[storage].[FileAssets] WITH (UPDLOCK", StringComparison.Ordinal))
            {
                Calls++;
                expire();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecoveryScope(MakanAppWebApplicationFactory factory, string connectionString) : IAsyncDisposable
    {
        public MakanAppWebApplicationFactory Factory => factory;
        public IFileStorage Storage => factory.Services.GetRequiredService<IFileStorage>();
        public DateTime Now => factory.Clock.GetUtcNow().UtcDateTime;
        public void AdvancePastLease() => factory.Clock.SetUtcNow(Now.AddMinutes(3));
        public MakanDbContext CreateDb(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer(connectionString).AddInterceptors(interceptors).Options);
        public ReportExportOutputStore Output(MakanDbContext db, IFileStorage storage, ILogger<ReportExportOutputStore>? logger = null) => new(db, storage,
            factory.Services.GetRequiredService<StorageOptions>(), factory.Clock,
            factory.Services.GetRequiredService<WorkerTelemetry>(), logger ?? NullLogger<ReportExportOutputStore>.Instance);
        public ReportExportProcessor Processor(MakanDbContext db, IFileStorage storage, IReportExporter exporter) => new(
            new EfReportExportJobStore(db, factory.Clock), new AllowedDataSource(), exporter, Output(db, storage),
            new ReportExportOptions(), factory.Clock, factory.Services.GetRequiredService<WorkerTelemetry>(),
            NullLogger<ReportExportProcessor>.Instance);
        public async Task<ReportExportJob> ClaimAsync(MakanDbContext db) =>
            (await new EfReportExportJobStore(db, factory.Clock).TryClaimNextAsync(Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None))!;

        public async Task<Guid[]> SeedJobsAsync(int count, bool exhausted = false, bool activeFinalLease = false)
        {
            await using var db = CreateDb();
            var user = User.Create(Now);
            var organization = MakanApp.Domain.Organization.Organization.Create("PH4 synthetic report recovery", Now);
            var membership = Membership.CreateActive(user.Id, organization.Id, Now);
            db.AddRange(user, organization, membership);
            var jobs = Enumerable.Range(0, count).Select(_ => ReportExportJob.Create(organization.Id, user.Id,
                Guid.NewGuid(), membership.Id, OrganizationRole.Manager, null, ReportExportType.ManagerAttendance,
                ReportExportFormat.Csv, Now, null, null, null, null, null, null, null, "classtitleasc", 0, Now)).ToArray();
            foreach (var job in jobs)
            {
                if (exhausted)
                {
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        var start = activeFinalLease && attempt == 2 ? Now : Now.AddMinutes(-10 + attempt * 2);
                        job.Claim(start, start.AddMinutes(2));
                    }
                }
                db.ReportExportJobs.Add(job);
            }
            await db.SaveChangesAsync();
            return jobs.Select(job => job.Id).ToArray();
        }

        public async Task<(ReportExportJob Job, FileAsset File, string Content)> ReadOutputAsync(Guid jobId)
        {
            await using var db = CreateDb();
            var job = await db.ReportExportJobs.AsNoTracking().SingleAsync(item => item.Id == jobId);
            var file = await db.FileAssets.AsNoTracking().SingleAsync(item => item.Id == jobId);
            await using var content = await Storage.OpenReadAsync(file.StorageKey, CancellationToken.None);
            using var reader = new StreamReader(content);
            return (job, file, await reader.ReadToEndAsync());
        }

        public static async Task<RecoveryScope> CreateAsync()
        {
            var id = Guid.NewGuid().ToString("N");
            var factory = new MakanAppWebApplicationFactory("MakanApp_Recovery_PH4_Reports_" + id,
                Path.Combine(Path.GetTempPath(), "MakanApp-StorageTests", "PH4-Reports-" + id),
                Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            try
            {
                await ((IAsyncLifetime)factory).InitializeAsync();
                factory.Clock.SetUtcNow(DateTime.UtcNow.AddDays(1));
                await using var scope = factory.Services.CreateAsyncScope();
                var connectionString = scope.ServiceProvider.GetRequiredService<MakanDbContext>().Database.GetConnectionString()!;
                return new RecoveryScope(factory, connectionString);
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
