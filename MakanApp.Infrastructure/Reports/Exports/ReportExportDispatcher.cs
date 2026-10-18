using System.Diagnostics;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Reports.Detailed;
using MakanApp.Application.Reports.Exports;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Observability;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Reports.Exports;

public sealed class ReportExportDispatcher(
    IServiceScopeFactory scopeFactory,
    IReportExportQueueSignal queueSignal,
    ReportExportOptions options,
    WorkerTelemetry telemetry,
    ILogger<ReportExportDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = false;
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.ReportExport))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    processed = await scope.ServiceProvider
                        .GetRequiredService<ReportExportProcessor>()
                        .ProcessNextAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException exception) when (exception.Number == 208)
                {
                    telemetry.RecordFailure(WorkerTelemetry.ReportExport, "Loop");
                    logger.LogDebug("جدول job خروجی گزارش هنوز آماده نیست.");
                }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.ReportExport, "Loop");
                    logger.LogError("حلقه پردازش خروجی گزارش ناموفق بود. Code={FailureCode}", "REPORT_EXPORT_LOOP_FAILED");
                }
                finally
                {
                    telemetry.RecordBatch(WorkerTelemetry.ReportExport, processed ? 1 : 0, started);
                }
            }

            if (!processed)
            {
                await queueSignal.WaitAsync(options.IdlePollingInterval, stoppingToken);
            }
        }
    }
}

public sealed class ReportExportProcessor(
    IReportExportJobStore jobStore,
    IReportExportDataSource dataSource,
    IReportExporter exporter,
    IReportExportOutputStore outputStore,
    ReportExportOptions options,
    TimeProvider timeProvider,
    WorkerTelemetry telemetry,
    ILogger<ReportExportProcessor> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var job = await jobStore.TryClaimNextAsync(
            nowUtc,
            options.ClaimLease,
            options.MaximumAttempts,
            cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            await dataSource.EnsureCurrentAccessAsync(
                job.RequestedByUserId,
                job.RequestedBySessionId,
                job,
                cancellationToken);
            var outputFileId = await outputStore.FindReusableOutputAsync(job, cancellationToken);
            if (!outputFileId.HasValue)
            {
                await using var generated = await exporter.GenerateAsync(job, cancellationToken);
                outputFileId = await outputStore.SaveAsync(
                    job,
                    generated,
                    options.MaximumFileSizeBytes,
                    cancellationToken);
            }

            job.Complete(outputFileId.Value, timeProvider.GetUtcNow().UtcDateTime);
            await jobStore.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsAuthorizationFailure(exception))
        {
            telemetry.RecordFailure(WorkerTelemetry.ReportExport, "Process");
            job.Fail(ReportExportErrorCodes.DataNoLongerAuthorized, timeProvider.GetUtcNow().UtcDateTime);
            await jobStore.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("دسترسی job خروجی گزارش {JobId} پیش از تولید معتبر نبود.", job.Id);
        }
        catch (ReportExportException exception) when (
            exception.Code is ReportExportErrorCodes.RowLimitExceeded or ReportExportErrorCodes.GenerationFailed)
        {
            telemetry.RecordFailure(WorkerTelemetry.ReportExport, "Process");
            job.Fail(exception.Code, timeProvider.GetUtcNow().UtcDateTime);
            await jobStore.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("تولید job خروجی گزارش {JobId} با کد {FailureCode} متوقف شد.", job.Id, exception.Code);
        }
        catch (Exception)
        {
            telemetry.RecordFailure(WorkerTelemetry.ReportExport, "Process");
            if (job.AttemptCount < options.MaximumAttempts)
            {
                job.ScheduleRetry(timeProvider.GetUtcNow().UtcDateTime.Add(options.RetryDelay));
            }
            else
            {
                job.Fail(ReportExportErrorCodes.GenerationFailed, timeProvider.GetUtcNow().UtcDateTime);
            }

            await jobStore.SaveChangesAsync(CancellationToken.None);
            logger.LogError("تولید job خروجی گزارش {JobId} ناموفق بود. Code={FailureCode}", job.Id, ReportExportErrorCodes.GenerationFailed);
        }

        return true;
    }

    private static bool IsAuthorizationFailure(Exception exception) =>
        exception is IdentityException or OrganizationException or GuardianException or DetailedReportException ||
        exception is ReportExportException reportException && reportException.Code == ReportExportErrorCodes.NotFound;
}
