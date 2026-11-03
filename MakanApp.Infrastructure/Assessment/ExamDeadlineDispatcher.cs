using System.Diagnostics;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Assessment;

public sealed class ExamDeadlineOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 50;
}

public sealed class ExamDeadlineDispatcher(IServiceScopeFactory scopes, ExamDeadlineOptions options,
    WorkerTelemetry telemetry, ILogger<ExamDeadlineDispatcher> logger, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var completed = 0;
            var started = Stopwatch.GetTimestamp();
            using (telemetry.BeginBatch(WorkerTelemetry.ExamDeadline))
            {
                try
                {
                    Guid[] ids;
                    await using (var scope = scopes.CreateAsyncScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
                        var now = clock.GetUtcNow().UtcDateTime;
                        ids = await db.ExamAttempts.AsNoTracking().Where(a => a.ContextKind == AcademicClassKind.PersonalClass &&
                                a.Status == ExamAttemptStatus.InProgress && a.EffectiveDeadlineUtc <= now &&
                                db.PersonalClasses.Any(c => c.Id == a.PersonalClassId && c.State == PersonalClassState.Active))
                            .OrderBy(a => a.EffectiveDeadlineUtc).ThenBy(a => a.Id).Take(options.BatchSize).Select(a => a.Id).ToArrayAsync(stoppingToken);
                    }
                    foreach (var id in ids)
                    {
                        try
                        {
                            await using var scope = scopes.CreateAsyncScope();
                            if (await scope.ServiceProvider.GetRequiredService<IExamDeadlineFinalizer>().FinalizeDueAttemptAsync(id, stoppingToken)) completed++;
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception)
                        {
                            telemetry.RecordFailure(WorkerTelemetry.ExamDeadline, "Process");
                            logger.LogError("نهایی‌سازی مهلت آزمون ناموفق بود. Code={ErrorCode} AttemptId={AttemptId}", "EXAM_DEADLINE_FINALIZATION_FAILED", id);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception)
                {
                    telemetry.RecordFailure(WorkerTelemetry.ExamDeadline, "Loop");
                    logger.LogError("حلقهٔ نهایی‌سازی آزمون ناموفق بود. Code={ErrorCode}", "EXAM_DEADLINE_LOOP_FAILED");
                }
                finally { telemetry.RecordBatch(WorkerTelemetry.ExamDeadline, completed, started); }
            }
            await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds), stoppingToken);
        }
    }
}
