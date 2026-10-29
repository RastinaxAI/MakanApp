using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public sealed record CreateOrderCommand(
    Guid PlanVersionId,
    Guid ClientOperationId,
    CommercialOrderPurpose Purpose = CommercialOrderPurpose.Purchase,
    Guid? SubscriptionId = null);

public sealed record InitiatePaymentCommand(Guid ClientOperationId);

public sealed record VerifyPaymentCommand(
    Guid PaymentAttemptId,
    string? ProviderReference);

public enum OrderFulfillmentState
{
    NotStarted = 1,
    SubscriptionActivationPending = 2,
    SubscriptionActivated = 3,
    ProvisioningPending = 4
}

public sealed record PaymentAttemptDto(
    Guid PaymentAttemptId,
    string Provider,
    PaymentAttemptStatus Status,
    decimal Amount,
    string Currency,
    string? ProviderReference,
    string? PaymentUrl,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record OrderDto(
    Guid OrderId,
    Guid? OrganizationId,
    Guid PlanVersionId,
    OrderStatus Status,
    decimal Amount,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    OrderFulfillmentState FulfillmentState,
    IReadOnlyCollection<PaymentAttemptDto> PaymentAttempts)
{
    public decimal PriceToman => Amount / 10m;
    public string DisplayCurrency => "TOMAN";
}

public sealed record CreateOrderResult(OrderDto Order, bool Replayed);

public sealed record InitiatePaymentResult(
    OrderDto Order,
    PaymentAttemptDto PaymentAttempt,
    bool Replayed);

public sealed record OrderListItemDto(
    Guid OrderId,
    Guid PlanVersionId,
    OrderStatus Status,
    decimal Amount,
    string Currency,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    OrderFulfillmentState FulfillmentState,
    PaymentAttemptStatus? LatestPaymentStatus)
{
    public decimal PriceToman => Amount / 10m;
    public string DisplayCurrency => "TOMAN";
}

public sealed record OrderListResult(IReadOnlyCollection<OrderListItemDto> Items);

public enum PaymentVerificationStatus
{
    Settled = 1,
    AlreadySettled = 2,
    Failed = 3,
    Unknown = 4,
    AmountMismatch = 5,
    CurrencyMismatch = 6,
    ReferenceConflict = 7
}

public sealed record VerifyPaymentResult(
    PaymentVerificationStatus Status,
    OrderDto Order,
    PaymentAttemptDto PaymentAttempt);
