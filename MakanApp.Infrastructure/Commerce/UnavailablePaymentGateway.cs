using MakanApp.Application.Commerce;

namespace MakanApp.Infrastructure.Commerce;

public sealed class UnavailablePaymentGateway : IPaymentGateway
{
    public string ProviderName => "Unavailable";

    public Task<PaymentGatewayCreationResult> CreatePaymentAsync(
        PaymentGatewayCreationRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentGatewayCreationResult(
            PaymentGatewayCreationOutcome.Unavailable,
            null,
            null,
            CommerceErrorCodes.PaymentProviderUnavailable));

    public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
        PaymentGatewayVerificationRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new PaymentGatewayVerificationResult(
            PaymentGatewayVerificationOutcome.Unavailable,
            request.ProviderReference,
            null,
            null,
            CommerceErrorCodes.PaymentProviderUnavailable));
}
