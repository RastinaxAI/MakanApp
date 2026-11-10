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
    string? FailureCode)
{
    public int? ProviderResult { get; init; }
}

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
    string? FailureCode)
{
    public string? SettlementReference { get; init; }
    public DateTime? ProviderPaidAtUtc { get; init; }
    public IReadOnlyList<PaymentProviderObservation> Observations { get; init; } = [];
}

public sealed record PaymentProviderObservation(bool IsInquiry, int? Result, int? Status);

public interface IPaymentGatewayResolver
{
    IPaymentGateway? Resolve(string providerName);
}

public interface IPaymentGateway
{
    bool IsConfigured { get; }
    string ProviderName { get; }

    Task<PaymentGatewayCreationResult> CreatePaymentAsync(
        PaymentGatewayCreationRequest request,
        CancellationToken cancellationToken);

    Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
        PaymentGatewayVerificationRequest request,
        CancellationToken cancellationToken);
}
