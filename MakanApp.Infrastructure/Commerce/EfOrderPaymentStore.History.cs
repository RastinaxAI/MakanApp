using System.Diagnostics;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfOrderPaymentStore
{
    public async Task RecordCallbackAsync(Guid attemptId, DateTime receivedAtUtc, int? reportedStatus, string? correlationId, CancellationToken cancellationToken)
    {
        var orderId = await FindPaymentOrderIdAsync(attemptId, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await LockPaymentOrderAsync(orderId, cancellationToken);
        var attempt = await dbContext.PaymentAttempts
            .FromSqlInterpolated($"SELECT * FROM [commerce].[PaymentAttempts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {attemptId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (attempt is null) return;
        attempt.RecordCallback(receivedAtUtc);
        dbContext.PaymentAttemptEvents.Add(PaymentAttemptEvent.Create(attemptId, PaymentAttemptEventType.CallbackReceived,
            receivedAtUtc, providerStatus: reportedStatus, correlationId: correlationId));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentHistoryDto>> GetPaymentHistoryAsync(Guid attemptId, CancellationToken cancellationToken) =>
        await dbContext.PaymentAttemptEvents.AsNoTracking().Where(e => e.PaymentAttemptId == attemptId)
            .OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Take(100)
            .Select(e => new PaymentHistoryDto(e.Id, e.Type, e.OccurredAtUtc, e.ProviderResult, e.ProviderStatus, e.ErrorCode, e.CorrelationId))
            .ToArrayAsync(cancellationToken);

    private async Task<Guid?> FindPaymentOrderIdAsync(Guid attemptId, CancellationToken cancellationToken) =>
        await dbContext.PaymentAttempts.AsNoTracking().Where(a => a.Id == attemptId).Select(a => (Guid?)a.OrderId).SingleOrDefaultAsync(cancellationToken);

    // همهٔ عملیات پرداخت ابتدا Order و سپس Attempt را قفل می‌کنند؛ تماس شبکه بیرون تراکنش است.
    private async Task LockPaymentOrderAsync(Guid? orderId, CancellationToken cancellationToken)
    {
        if (orderId.HasValue) _ = await dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {orderId.Value}")
            .SingleAsync(cancellationToken);
    }

    private void AddEvent(Guid attemptId, PaymentAttemptEventType type, DateTime occurredAtUtc, int? result = null, int? status = null, string? errorCode = null) =>
        dbContext.PaymentAttemptEvents.Add(PaymentAttemptEvent.Create(attemptId, type, occurredAtUtc, result, status,
            errorCode, Activity.Current?.TraceId.ToString()));
}
