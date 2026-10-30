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
        using var outcomes = new OutcomeCapture(isolated.Factory);
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
        Assert.Equal(1, outcomes.Count("Claimed"));
        Assert.Equal(0, outcomes.Count("Completed"));
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
        using var outcomes = new OutcomeCapture(isolated.Factory);
        _ = await isolated.SeedJobsAsync(1);
        await using var expiredDb = isolated.CreateDb();
        var job = await isolated.ClaimAsync(expiredDb);
        var claimVersion = job.RowVersion.ToArray();
        isolated.AdvancePastLease();
        var storage = new ObservedStorage(isolated.Storage);
        await using var generated = TextFile("expired candidate");
        Assert.Null(await isolated.Output(expiredDb, storage).SaveAsync(job, claimVersion, generated, 1024, CancellationToken.None));
        job.Fail(ReportExportErrorCodes.GenerationFailed, isolated.Now);
        Assert.False(await isolated.Store(expiredDb)
            .TrySaveClaimChangesAsync(job, claimVersion, CancellationToken.None));
        Assert.Equal(1, outcomes.Count("LeaseLost"));
        Assert.Equal(0, outcomes.Count("Failed"));
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
        using var outcomes = new OutcomeCapture(isolated.Factory);
        var logger = new StoreLogger();
        _ = await isolated.SeedJobsAsync(EfReportExportJobStore.MaximumRepairBatchSize + 1, exhausted: true);
        var activeId = Assert.Single(await isolated.SeedJobsAsync(1, exhausted: true, activeFinalLease: true));
        await using var claimDb = isolated.CreateDb();
        var store = isolated.Store(claimDb, logger);
        Assert.Null(await store.TryClaimNextAsync(isolated.Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None));
        Assert.Equal(EfReportExportJobStore.MaximumRepairBatchSize, outcomes.Count("Failed"));
        await using (var verify = isolated.CreateDb())
        {
            Assert.Equal(EfReportExportJobStore.MaximumRepairBatchSize, await verify.ReportExportJobs.CountAsync(job => job.Status == ReportExportJobStatus.Failed));
            Assert.Equal(ReportExportJobStatus.Processing, (await verify.ReportExportJobs.SingleAsync(job => job.Id == activeId)).Status);
        }

        Assert.Null(await store.TryClaimNextAsync(isolated.Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None));
        Assert.Equal(EfReportExportJobStore.MaximumRepairBatchSize + 1, outcomes.Count("Failed"));
        await using (var verify = isolated.CreateDb())
            Assert.Equal(EfReportExportJobStore.MaximumRepairBatchSize + 1, await verify.ReportExportJobs.CountAsync(job => job.Status == ReportExportJobStatus.Failed));
        isolated.AdvancePastLease();
        Assert.Null(await store.TryClaimNextAsync(isolated.Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None));
        Assert.Equal(EfReportExportJobStore.MaximumRepairBatchSize + 2, outcomes.Count("Failed"));
        Assert.Equal(0, outcomes.Count("Claimed"));
        Assert.Equal(3, logger.Entries.Count);
        Assert.All(logger.Entries, entry =>
        {
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Null(entry.Exception);
            Assert.Contains("REPORT_EXPORT_EXHAUSTED_REPAIR", entry.Message, StringComparison.Ordinal);
        });
        await using var final = isolated.CreateDb();
        Assert.Equal(ReportExportJobStatus.Failed, (await final.ReportExportJobs.SingleAsync(job => job.Id == activeId)).Status);
    }

    [Fact]
    public async Task TransientFailuresRespectEveryFixedBackoffAndBecomeTerminalAtMaximumAttempts()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        using var outcomes = new OutcomeCapture(isolated.Factory);
        var exporter = new TransientExporter(int.MaxValue);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var attemptedAt = isolated.Now;
            await using (var processing = isolated.CreateDb())
                Assert.True(await isolated.Processor(processing, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None));
            var job = await isolated.ReadJobAsync(jobId);
            Assert.Equal(attempt, job.AttemptCount);
            if (attempt < 3)
            {
                Assert.Equal(ReportExportJobStatus.Queued, job.Status);
                Assert.Equal(attemptedAt.AddSeconds(10), job.NextAttemptAtUtc);
                await using (var early = isolated.CreateDb())
                    Assert.False(await isolated.Processor(early, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None));
                isolated.Factory.Clock.SetUtcNow(attemptedAt.AddSeconds(9));
                await using (var stillEarly = isolated.CreateDb())
                    Assert.False(await isolated.Processor(stillEarly, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None));
                Assert.Equal(attempt, exporter.Calls);
                isolated.Factory.Clock.SetUtcNow(attemptedAt.AddSeconds(10));
            }
            else
            {
                Assert.Equal(ReportExportJobStatus.Failed, job.Status);
                Assert.Equal(ReportExportErrorCodes.GenerationFailed, job.FailureCode);
                Assert.Null(job.NextAttemptAtUtc);
                Assert.Null(job.LeaseExpiresAtUtc);
            }
        }

        await using var terminalPoll = isolated.CreateDb();
        Assert.False(await isolated.Processor(terminalPoll, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None));
        Assert.Equal(3, exporter.Calls);
        Assert.Equal(3, outcomes.Count("Claimed"));
        Assert.Equal(2, outcomes.Count("RetryScheduled"));
        Assert.Equal(1, outcomes.Count("Failed"));
        Assert.Equal(0, outcomes.Count("Completed"));
        Assert.Empty(await terminalPoll.FileAssets.ToArrayAsync());
    }

    [Fact]
    public async Task PermanentPoisonJobDoesNotPreventMultipleFollowingHealthyJobsFromCompleting()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        _ = await isolated.SeedJobsAsync(4);
        Guid poisonId;
        await using (var ordering = isolated.CreateDb())
            poisonId = await ordering.ReportExportJobs.OrderBy(job => job.RequestedAtUtc).ThenBy(job => job.Id).Select(job => job.Id).FirstAsync();
        using var outcomes = new OutcomeCapture(isolated.Factory);
        var exporter = new SelectivePoisonExporter(poisonId);
        var processed = 0;
        while (processed < 5)
        {
            await using var db = isolated.CreateDb();
            if (!await isolated.Processor(db, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None)) break;
            processed++;
        }

        Assert.Equal(4, processed);
        await using var verify = isolated.CreateDb();
        var jobs = await verify.ReportExportJobs.AsNoTracking().ToArrayAsync();
        var poison = Assert.Single(jobs, job => job.Id == poisonId);
        Assert.Equal(ReportExportJobStatus.Failed, poison.Status);
        Assert.Equal(1, poison.AttemptCount);
        Assert.Equal(ReportExportErrorCodes.GenerationFailed, poison.FailureCode);
        Assert.All(jobs.Where(job => job.Id != poisonId), job => Assert.Equal(ReportExportJobStatus.Completed, job.Status));
        Assert.Equal(3, await verify.FileAssets.CountAsync());
        Assert.Equal(4, outcomes.Count("Claimed"));
        Assert.Equal(3, outcomes.Count("Completed"));
        Assert.Equal(1, outcomes.Count("Failed"));
        Assert.Equal(0, outcomes.Count("RetryScheduled"));
    }

    [Fact]
    public async Task HealthyDependencyAfterTransientFailureCompletesTheSameDurableJobExactlyOnce()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        using var outcomes = new OutcomeCapture(isolated.Factory);
        var exporter = new TransientExporter(1);
        await using (var first = isolated.CreateDb())
            Assert.True(await isolated.Processor(first, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None));
        var retry = await isolated.ReadJobAsync(jobId);
        Assert.Equal(ReportExportJobStatus.Queued, retry.Status);
        isolated.Factory.Clock.SetUtcNow(DateTime.SpecifyKind(retry.NextAttemptAtUtc!.Value, DateTimeKind.Utc));
        await using (var recovered = isolated.CreateDb())
            Assert.True(await isolated.Processor(recovered, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None));
        var output = await isolated.ReadOutputAsync(jobId);
        Assert.Equal(ReportExportJobStatus.Completed, output.Job.Status);
        Assert.Equal(2, output.Job.AttemptCount);
        Assert.Equal("healthy after transient failure", output.Content);
        await using var completedPoll = isolated.CreateDb();
        Assert.False(await isolated.Processor(completedPoll, isolated.Storage, exporter).ProcessNextAsync(CancellationToken.None));
        Assert.Equal(1, await completedPoll.FileAssets.CountAsync());
        Assert.Equal(2, exporter.Calls);
        Assert.Equal(2, outcomes.Count("Claimed"));
        Assert.Equal(1, outcomes.Count("RetryScheduled"));
        Assert.Equal(1, outcomes.Count("Completed"));
        Assert.Equal(0, outcomes.Count("Failed"));
    }

    [Fact]
    public async Task LostClaimCommitAcknowledgementDoesNotInventAConfirmedMetricAndCanRecover()
    {
        await using var isolated = await RecoveryScope.CreateAsync();
        var jobId = Assert.Single(await isolated.SeedJobsAsync(1));
        using var outcomes = new OutcomeCapture(isolated.Factory);
        await using (var uncertain = isolated.CreateDb(new CommitAcknowledgementFailure(1)))
            await Assert.ThrowsAsync<IOException>(() => isolated.Store(uncertain).TryClaimNextAsync(
                isolated.Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None));
        Assert.Equal(ReportExportJobStatus.Processing, (await isolated.ReadJobAsync(jobId)).Status);
        Assert.Equal(0, outcomes.Count("Claimed"));
        isolated.AdvancePastLease();
        await using (var recovered = isolated.CreateDb())
            Assert.True(await isolated.Processor(recovered, isolated.Storage, new TextExporter("recovered after uncertain claim"))
                .ProcessNextAsync(CancellationToken.None));
        Assert.Equal(2, (await isolated.ReadJobAsync(jobId)).AttemptCount);
        Assert.Equal(1, outcomes.Count("Claimed"));
        Assert.Equal(1, outcomes.Count("LeaseRecovered"));
        Assert.Equal(1, outcomes.Count("Completed"));
    }

    private static GeneratedReportFile TextFile(string text) => new(new MemoryStream(Encoding.UTF8.GetBytes(text)), "text/csv", "report.csv");

    private sealed class TransientExporter(int failuresBeforeHealthy) : IReportExporter
    {
        public int Calls { get; private set; }
        public Task<GeneratedReportFile> GenerateAsync(ReportExportJob job, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (Calls <= failuresBeforeHealthy) throw new IOException("synthetic-private-report-dependency-marker");
            return Task.FromResult(TextFile("healthy after transient failure"));
        }
    }

    private sealed class SelectivePoisonExporter(Guid poisonId) : IReportExporter
    {
        public Task<GeneratedReportFile> GenerateAsync(ReportExportJob job, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (job.Id == poisonId)
                throw new ReportExportException(ReportExportErrorCodes.GenerationFailed, "synthetic-private-poison-marker");
            return Task.FromResult(TextFile("healthy following poison"));
        }
    }

    private sealed record OutcomeReading(string Outcome, long Count);
    private sealed class OutcomeCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentQueue<OutcomeReading> _readings = new();
        public OutcomeCapture(MakanAppWebApplicationFactory factory)
        {
            var meter = factory.Services.GetRequiredService<IMeterFactory>().Create(WorkerTelemetry.MeterName);
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (ReferenceEquals(instrument.Meter, meter) && instrument.Name == "makan.worker.outcomes")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, count, tags, _) =>
            {
                var values = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value);
                if (Equals(WorkerTelemetry.ReportExport, values.GetValueOrDefault("worker")))
                    _readings.Enqueue(new(values["outcome"]!.ToString()!, count));
            });
            _listener.Start();
        }
        public long Count(string outcome) => _readings.Where(reading => reading.Outcome == outcome).Sum(reading => reading.Count);
        public void Dispose() => _listener.Dispose();
    }

    private sealed record StoreLog(LogLevel Level, string Message, Exception? Exception);
    private sealed class StoreLogger : ILogger<EfReportExportJobStore>
    {
        public List<StoreLog> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new(logLevel, formatter(state, exception), exception));
    }

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
            Store(db), new AllowedDataSource(), exporter, Output(db, storage),
            new ReportExportOptions(), factory.Clock, factory.Services.GetRequiredService<WorkerTelemetry>(),
            NullLogger<ReportExportProcessor>.Instance);
        public async Task<ReportExportJob> ClaimAsync(MakanDbContext db) =>
            (await Store(db).TryClaimNextAsync(Now, TimeSpan.FromMinutes(2), 3, CancellationToken.None))!;
        public EfReportExportJobStore Store(MakanDbContext db, ILogger<EfReportExportJobStore>? logger = null) =>
            new(db, new MakanApp.Infrastructure.Commerce.EfCommercialStore(db,
                new MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter(db,
                    new MakanApp.Infrastructure.Outbox.EfOutbox(db), factory.Clock)), factory.Clock,
                factory.Services.GetRequiredService<WorkerTelemetry>(), logger ?? NullLogger<EfReportExportJobStore>.Instance);

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

        public async Task<ReportExportJob> ReadJobAsync(Guid jobId)
        {
            await using var db = CreateDb();
            return await db.ReportExportJobs.AsNoTracking().SingleAsync(job => job.Id == jobId);
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
