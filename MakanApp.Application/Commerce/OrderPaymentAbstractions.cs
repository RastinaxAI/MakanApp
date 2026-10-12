using MakanApp.Application.Outbox;
using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public interface IOrderPaymentService
{
    Task<CreateOrderResult> CreateOrderAsync(
        Guid userId,
        Guid sessionId,
        CreateOrderCommand command,
        CancellationToken cancellationToken);

    Task<InitiatePaymentResult> InitiatePaymentAsync(
        Guid userId,
        Guid sessionId,
        Guid orderId,
        InitiatePaymentCommand command,
        CancellationToken cancellationToken);

    Task<OrderDto> GetOrderAsync(
        Guid userId,
        Guid sessionId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<OrderListResult> ListOrdersAsync(
        Guid userId,
        Guid sessionId,
        int limit,
        CancellationToken cancellationToken);
}

public interface IPaymentVerificationService
{
    Task<VerifyPaymentResult> VerifyPaymentAsync(
        VerifyPaymentCommand command,
        CancellationToken cancellationToken);
}

public sealed record PaymentAttemptSnapshot(
    Guid Id,
    Guid OrderId,
    Guid? OrganizationId,
    Guid PlanVersionId,
    string Provider,
    PaymentAttemptStatus Status,
    decimal Amount,
    CurrencyCode Currency,
    string? ProviderReference,
    string? PaymentUrl,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record OrderPaymentSnapshot(
    Guid Id,
    Guid PurchaserUserId,
    Guid? OrganizationId,
    Guid PlanVersionId,
    OrderStatus Status,
    decimal Amount,
    CurrencyCode Currency,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    OrderFulfillmentState FulfillmentState,
    IReadOnlyCollection<PaymentAttemptSnapshot> PaymentAttempts);

public enum CreateOrderStoreStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyConflict = 3,
    PlanNotPurchasable = 4
}

public sealed record CreateOrderStoreResult(
    CreateOrderStoreStatus Status,
    OrderPaymentSnapshot? Order);

public enum CreatePaymentAttemptStoreStatus
{
    Created = 1,
    Replayed = 2,
    IdempotencyConflict = 3,
    OrderNotFound = 4,
    OrderNotPayable = 5
}

public sealed record CreatePaymentAttemptStoreResult(
    CreatePaymentAttemptStoreStatus Status,
    OrderPaymentSnapshot? Order,
    PaymentAttemptSnapshot? Attempt);

public enum CompletePaymentInitiationStoreStatus
{
    Completed = 1,
    StaleClaim = 2,
    ReferenceConflict = 3
}

public sealed record CompletePaymentInitiationStoreResult(
    CompletePaymentInitiationStoreStatus Status,
    OrderPaymentSnapshot? Order,
    PaymentAttemptSnapshot? Attempt);

public enum RecordPaymentVerificationStoreStatus
{
    Settled = 1,
    AlreadySettled = 2,
    Failed = 3,
    Unknown = 4,
    AmountMismatch = 5,
    CurrencyMismatch = 6,
    ReferenceConflict = 7,
    AttemptNotFound = 8,
    OrderNotPayable = 9
}

public sealed record RecordPaymentVerificationStoreResult(
    RecordPaymentVerificationStoreStatus Status,
    OrderPaymentSnapshot? Order,
    PaymentAttemptSnapshot? Attempt);

public interface IOrderPaymentStore
{
    Task<CreateOrderStoreResult> CreateOrderAsync(
        Guid purchaserUserId,
        Guid organizationId,
        Guid planVersionId,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);

    Task<OrderPaymentSnapshot?> GetOrderAsync(
        Guid organizationId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrderPaymentSnapshot>> ListOrdersAsync(
        Guid organizationId,
        int limit,
        CancellationToken cancellationToken);

    Task<CreatePaymentAttemptStoreResult> CreatePaymentAttemptAsync(
        Guid organizationId,
        Guid orderId,
        string provider,
        Guid clientOperationId,
        byte[] requestPayloadHash,
        DateTime createdAtUtc,
        CancellationToken cancellationToken);

    Task<bool> TryClaimPaymentInitiationAsync(
        Guid paymentAttemptId,
        Guid claimToken,
        DateTime nowUtc,
        DateTime claimedUntilUtc,
        CancellationToken cancellationToken);

    Task<CompletePaymentInitiationStoreResult> CompletePaymentInitiationAsync(
        Guid paymentAttemptId,
        Guid claimToken,
        PaymentGatewayCreationResult providerResult,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken);

    Task<PaymentAttemptSnapshot?> GetPaymentAttemptAsync(
        Guid paymentAttemptId,
        CancellationToken cancellationToken);

    Task<RecordPaymentVerificationStoreResult> RecordPaymentVerificationAsync(
        Guid paymentAttemptId,
        PaymentGatewayVerificationResult providerResult,
        DateTime verifiedAtUtc,
        OutboxEnvelope? subscriptionActivationMessage,
        CancellationToken cancellationToken);
}

public interface ISubscriptionActivationHandler
{
    Task HandleAsync(
        SubscriptionActivationRequested payload,
        CancellationToken cancellationToken);
}
