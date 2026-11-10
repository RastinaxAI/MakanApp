using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public sealed record PaymentHistoryDto(Guid EventId, PaymentAttemptEventType Type, DateTime OccurredAtUtc,
    int? ProviderResult, int? ProviderStatus, string? ErrorCode, string? CorrelationId);
public sealed record PaymentAttemptDetailsDto(OrderDto Order, PaymentAttemptDto PaymentAttempt, IReadOnlyList<PaymentHistoryDto> History);
public sealed record PaymentReceiptDto(Guid OrderId, Guid PurchaserUserId, Guid? OrganizationId, Guid PlanVersionId,
    string? ProductTitle, decimal Amount, string Currency, DateTime PaidAtUtc, OrderFulfillmentState FulfillmentState,
    string? Provider, string? ProviderReference, string? SettlementReference, DateTime? ProviderPaidAtUtc,
    CommercialOrderPurpose Purpose, DateTime? BillingAnchorUtc, int BillingPeriodIndex);
public sealed record PaymentCallbackCommand(string Provider, Guid PaymentAttemptId, string ProviderReference, int? ReportedStatus);
public sealed record PaymentCallbackResult(Guid OrderId, Guid PaymentAttemptId);

public interface IPaymentRecoveryService
{
    Task<PaymentAttemptDetailsDto> GetAttemptAsync(Guid userId, Guid sessionId, Guid attemptId, CancellationToken cancellationToken);
    Task<VerifyPaymentResult> RecoverAsync(Guid userId, Guid sessionId, Guid attemptId, CancellationToken cancellationToken);
    Task<PaymentCallbackResult> HandleCallbackAsync(PaymentCallbackCommand command, CancellationToken cancellationToken);
    Task<PaymentReceiptDto> GetReceiptAsync(Guid userId, Guid sessionId, Guid orderId, SubscriptionOwnerType ownerType, CancellationToken cancellationToken);
}
