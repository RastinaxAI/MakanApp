using System.Data;
using MakanApp.Application.Identity;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Hosting;
using MakanApp.Infrastructure.Observability;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Identity;

public sealed record AccountDeletionClaim(Guid RequestId, Guid Token);

public sealed class AccountDeletionProcessor(MakanDbContext db, AccountDeletionOptions options, AccountDeletionWorkflow workflow,
    IAccountDeletionKnowledgePurger knowledge, TimeProvider clock, WorkerTelemetry telemetry, ILogger<AccountDeletionProcessor> logger)
{
    public const int BatchSize = 10;
    public const int MaximumFailures = 10;
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);
    private static readonly HashSet<string> SafeFailureCodes = typeof(AccountDeletionErrorCodes)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
        .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    public async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        if (!options.ExecutionEnabled) return 0;
        await ScheduleRequestedAsync(ct);
        var processed = 0;
        while (processed < BatchSize)
        {
            var claim = await ClaimNextAsync(ct);
            if (claim is null) break;
            processed++;
            await ProcessClaimAsync(claim, ct);
        }
        return processed;
    }

    public async Task<AccountDeletionClaim?> ClaimNextAsync(CancellationToken ct)
    {
        if (!options.ExecutionEnabled) return null;
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.AccountDeletionRequests.FromSqlInterpolated($"""
            SELECT TOP (1) * FROM [identity].[AccountDeletionRequests] WITH (UPDLOCK,READPAST,READCOMMITTEDLOCK)
            WHERE [Status] IN (2,3) AND [ScheduledExecutionAtUtc]<={now}
              AND ([NextAttemptAtUtc] IS NULL OR [NextAttemptAtUtc]<={now})
              AND ([ClaimedUntilUtc] IS NULL OR [ClaimedUntilUtc]<={now})
            ORDER BY [ScheduledExecutionAtUtc],[Id]
            """).ToArrayAsync(ct);
        if (rows.Length == 0) { await tx.CommitAsync(ct); return null; }
        var request = rows[0]; var token = Guid.NewGuid(); var recovered = request.ClaimToken.HasValue;
        request.Claim(token, now, ClaimLease);
        if (request.ConsecutiveFailures >= MaximumFailures)
        {
            var step = await db.AccountDeletionSteps.Where(s => s.RequestId == request.Id && s.Status != AccountDeletionStepStatus.Completed)
                .OrderBy(s => s.Step).FirstAsync(ct);
            step.Fail(AccountDeletionErrorCodes.RecoveryExhausted);
            request.Fail(token, step.Step, AccountDeletionErrorCodes.RecoveryExhausted, false, now, MaximumFailures, failureAlreadyCounted: true);
            Audit(request, "DeletionNeedsAttention", step.Step, AccountDeletionErrorCodes.RecoveryExhausted, now);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
            RecordFailure(request.Id, step.Step, AccountDeletionErrorCodes.RecoveryExhausted);
            return null;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        telemetry.RecordOutcome(WorkerTelemetry.AccountDeletion, recovered ? "LeaseRecovered" : "Claimed");
        return new(request.Id, token);
    }

    public async Task ProcessClaimAsync(AccountDeletionClaim claim, CancellationToken ct)
    {
        if (!options.ExecutionEnabled) return;
        AccountDeletionStepKind? kind = null;
        try
        {
            var execution = await StartStepAsync(claim, ct); kind = execution.Step;
            if (kind == AccountDeletionStepKind.DeletePersonalKnowledge)
                await ProcessKnowledgeAsync(claim, execution.Context, ct);
            else
                await ProcessSqlAsync(claim, execution.Context, kind.Value, ct);
        }
        catch (AccountDeletionClaimLostException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("مالکیت اجرای حذف منتقل شده است. RequestId={RequestId}; CorrelationId={CorrelationId}; Code={FailureCode}",
                claim.RequestId, Correlation(claim.RequestId), "ACCOUNT_DELETION_CLAIM_LOST");
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            var (code, retryable) = Failure(exception);
            using var budget = WorkerCompletionBudget.Create();
            try { await SaveFailureAsync(claim, kind, code, retryable, budget.Token); }
            catch (AccountDeletionClaimLostException)
            { logger.LogWarning("ثبت شکست به مالک جدید واگذار شد. RequestId={RequestId}; Code={FailureCode}", claim.RequestId, code); }
            catch (Exception)
            {
                telemetry.RecordFailure(WorkerTelemetry.AccountDeletion, "Process");
                logger.LogError("ثبت checkpoint حذف حساب ناموفق بود. RequestId={RequestId}; CorrelationId={CorrelationId}; Code={FailureCode}",
                    claim.RequestId, Correlation(claim.RequestId), "ACCOUNT_DELETION_CHECKPOINT_UNAVAILABLE");
                throw;
            }
            RecordFailure(claim.RequestId, kind, code);
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        }
    }

    private async Task ScheduleRequestedAsync(CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var rows = await db.AccountDeletionRequests.FromSqlInterpolated($"""
            SELECT TOP ({BatchSize}) * FROM [identity].[AccountDeletionRequests] WITH (UPDLOCK,READPAST,READCOMMITTEDLOCK)
            WHERE [Status]=1 ORDER BY [RequestedAtUtc],[Id]
            """).ToArrayAsync(ct);
        foreach (var request in rows)
        {
            request.Schedule(options.ScheduleAt(request.RequestedAtUtc));
            Audit(request, "DeletionScheduled", null, request.PolicyVersion, clock.GetUtcNow().UtcDateTime);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
    }

    private async Task<(AccountDeletionStepKind Step, AccountDeletionExecutionContext Context)> StartStepAsync(AccountDeletionClaim claim, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var request = await LockClaimAsync(claim, ct);
        if (request.PolicyVersion != AccountDeletionRequest.CurrentPolicyVersion)
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
        var step = await db.AccountDeletionSteps.Where(s => s.RequestId == request.Id && s.Status != AccountDeletionStepStatus.Completed)
            .OrderBy(s => s.Step).FirstOrDefaultAsync(ct)
            ?? throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
        step.Start(clock.GetUtcNow().UtcDateTime);
        var context = new AccountDeletionExecutionContext(request.Id, request.UserId, request.RequestedBySessionId, claim.Token, request.PolicyVersion);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        return (step.Step, context);
    }

    private async Task ProcessSqlAsync(AccountDeletionClaim claim, AccountDeletionExecutionContext context, AccountDeletionStepKind kind, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var request = await LockClaimAsync(claim, ct);
        await workflow.ExecuteSqlStepAsync(kind, context, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        if (kind == AccountDeletionStepKind.FreezeAccount && !request.StartedAtUtc.HasValue)
        { request.MarkStarted(claim.Token, now); Audit(request, "DeletionStarted", kind, request.PolicyVersion, now); }
        await CompleteStepAsync(request, kind, claim.Token, now, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        telemetry.RecordOutcome(WorkerTelemetry.AccountDeletion, "Completed");
    }

    private async Task ProcessKnowledgeAsync(AccountDeletionClaim claim, AccountDeletionExecutionContext context, CancellationToken ct)
    {
        AccountDeletionKnowledgeWork? work;
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            var request = await LockClaimAsync(claim, ct);
            work = await knowledge.PrepareNextAsync(context, ct);
            if (work is null)
            {
                await knowledge.VerifyAsync(context, ct);
                await CompleteStepAsync(request, AccountDeletionStepKind.DeletePersonalKnowledge, claim.Token, clock.GetUtcNow().UtcDateTime, ct);
            }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        }
        if (work is null) return;
        // قطع فرایند بعد از پاسخ شبکه ممکن است همان command را تکرار کند؛ شناسهٔ پایدار و delete idempotent الزامی‌اند.
        var outcome = await knowledge.DeleteAsync(work, ct);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
        {
            var request = await LockClaimAsync(claim, ct);
            await knowledge.CompleteAsync(work, outcome, ct);
            request.ReleaseProgress(claim.Token, clock.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
        }
    }

    private async Task CompleteStepAsync(AccountDeletionRequest request, AccountDeletionStepKind kind, Guid token, DateTime now, CancellationToken ct)
    {
        request.EnsureClaim(token, now);
        var step = await db.AccountDeletionSteps.SingleAsync(s => s.RequestId == request.Id && s.Step == kind, ct);
        step.Complete(now); Audit(request, "DeletionStepCompleted", kind, request.PolicyVersion, now);
        if (kind == AccountDeletionStepKind.Finalize)
        {
            var steps = await db.AccountDeletionSteps.Where(s => s.RequestId == request.Id).ToArrayAsync(ct);
            request.Complete(token, now, steps); Audit(request, "DeletionCompleted", kind, request.PolicyVersion, now);
        }
        else request.ReleaseProgress(token, now);
    }

    private async Task SaveFailureAsync(AccountDeletionClaim claim, AccountDeletionStepKind? kind, string code, bool retryable, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var request = await LockClaimAsync(claim, ct);
        var step = await db.AccountDeletionSteps.Where(s => s.RequestId == request.Id && s.Status != AccountDeletionStepStatus.Completed)
            .OrderBy(s => s.Step).FirstAsync(ct);
        if (kind.HasValue && kind != step.Step) throw new AccountDeletionClaimLostException();
        step.Fail(code); var now = clock.GetUtcNow().UtcDateTime;
        request.Fail(claim.Token, step.Step, code, retryable, now, MaximumFailures);
        if (request.Status == AccountDeletionStatus.NeedsAttention) Audit(request, "DeletionNeedsAttention", step.Step, code, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); db.ChangeTracker.Clear();
    }

    private async Task<AccountDeletionRequest> LockClaimAsync(AccountDeletionClaim claim, CancellationToken ct)
    {
        var request = await db.AccountDeletionRequests.FromSqlInterpolated($"SELECT * FROM [identity].[AccountDeletionRequests] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={claim.RequestId}")
            .SingleOrDefaultAsync(ct) ?? throw new AccountDeletionClaimLostException();
        request.EnsureClaim(claim.Token, clock.GetUtcNow().UtcDateTime); return request;
    }

    private void Audit(AccountDeletionRequest request, string action, AccountDeletionStepKind? step, string code, DateTime now) =>
        db.AdminAuditLogs.Add(AdminAuditLog.Create(null, null, null, "privacy." + action, "AccountDeletionRequest", request.Id.ToString("D"), null,
            request.PolicyVersion, now, Correlation(request.Id), action == "DeletionNeedsAttention" ? AdminAuditResult.Failed : AdminAuditResult.Succeeded,
            null, step.HasValue ? $"Step={step};Code={code}" : null));

    private void RecordFailure(Guid request, AccountDeletionStepKind? step, string code)
    {
        telemetry.RecordFailure(WorkerTelemetry.AccountDeletion, "Process");
        logger.LogError("اجرای حذف حساب تکمیل نشد. RequestId={RequestId}; CorrelationId={CorrelationId}; Step={Step}; Code={FailureCode}",
            request, Correlation(request), step?.ToString() ?? "Claim", code);
    }
    private static string Correlation(Guid id) => "account-deletion-" + id.ToString("N");
    private static (string Code, bool Retryable) Failure(Exception exception) => exception switch
    {
        AccountDeletionExecutionException failure when SafeFailureCodes.Contains(failure.Code) => (failure.Code, failure.Retryable),
        OperationCanceledException => (AccountDeletionErrorCodes.StepFailed, true),
        SqlException { Number: 1205 or -2 or 1222 } => (AccountDeletionErrorCodes.StepFailed, true),
        DbUpdateException { InnerException: SqlException { Number: 1205 or -2 or 1222 } } => (AccountDeletionErrorCodes.StepFailed, true),
        DbUpdateConcurrencyException => (AccountDeletionErrorCodes.StepFailed, true),
        DbUpdateException => (AccountDeletionErrorCodes.ReferentialConflict, false),
        _ => (AccountDeletionErrorCodes.StepFailed, false)
    };
}
