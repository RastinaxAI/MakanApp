using MakanApp.Domain.Commerce;

namespace MakanApp.Application.Commerce;

public enum PaymentGatewayCreationOutcome
{
    PendingVerification = 1,
    Failed = 2,
    Unknown = 3,
    Unavailable = 4
}

public enum PaymentGatewayVerificationOutcome
{
    Succeeded = 1,
    Failed = 2,
    Unknown = 3,
    Unavailable = 4
}

public sealed record PaymentGatewayCreationRequest(
    Guid PaymentAttemptId,
    Guid OrderId,
    decimal Amount,
    CurrencyCode Currency,
    string Description);

public sealed record PaymentGatewayCreationResult(
    PaymentGatewayCreationOutcome Outcome,
    string? ProviderReference,
    string? PaymentUrl,
    string? FailureCode);

public sealed record PaymentGatewayVerificationRequest(
    Guid PaymentAttemptId,
    Guid OrderId,
    decimal ExpectedAmount,
    CurrencyCode ExpectedCurrency,
    string? ProviderReference);

public sealed record PaymentGatewayVerificationResult(
    PaymentGatewayVerificationOutcome Outcome,
    string? ProviderReference,
    decimal? VerifiedAmount,
    CurrencyCode? VerifiedCurrency,
    string? FailureCode);

public interface IPaymentGateway
{
    string ProviderName { get; }

    Task<PaymentGatewayCreationResult> CreatePaymentAsync(
        PaymentGatewayCreationRequest request,
        CancellationToken cancellationToken);

    Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
        PaymentGatewayVerificationRequest request,
        CancellationToken cancellationToken);
}
