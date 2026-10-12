using System.Collections.Concurrent;
using MakanApp.Application.Commerce;

namespace MakanApp.IntegrationTests;

public enum PaymentCreationBehavior
{
    Pending = 1,
    Failed = 2,
    Timeout = 3
}

public sealed class PaymentGatewayTestControl
{
    private readonly ConcurrentQueue<PaymentCreationBehavior> _creationBehaviors = new();
    private readonly ConcurrentQueue<PaymentGatewayCreationRequest> _creationRequests = new();
    private readonly ConcurrentQueue<PaymentGatewayVerificationRequest> _verificationRequests = new();
    private PaymentGatewayVerificationResult? _verificationResult;

    public string? CreationProviderReference { get; set; }

    public IReadOnlyCollection<PaymentGatewayCreationRequest> CreationRequests =>
        _creationRequests.ToArray();

    public IReadOnlyCollection<PaymentGatewayVerificationRequest> VerificationRequests =>
        _verificationRequests.ToArray();

    public void EnqueueCreation(PaymentCreationBehavior behavior) =>
        _creationBehaviors.Enqueue(behavior);

    public void SetVerificationResult(PaymentGatewayVerificationResult result) =>
        _verificationResult = result;

    public void Reset()
    {
        while (_creationBehaviors.TryDequeue(out _))
        {
        }

        while (_creationRequests.TryDequeue(out _))
        {
        }

        while (_verificationRequests.TryDequeue(out _))
        {
        }

        _verificationResult = null;
        CreationProviderReference = null;
    }

    internal PaymentCreationBehavior NextCreation() =>
        _creationBehaviors.TryDequeue(out var behavior)
            ? behavior
            : PaymentCreationBehavior.Pending;

    internal void Record(PaymentGatewayCreationRequest request) =>
        _creationRequests.Enqueue(request);

    internal void Record(PaymentGatewayVerificationRequest request) =>
        _verificationRequests.Enqueue(request);

    internal PaymentGatewayVerificationResult VerificationResult(
        PaymentGatewayVerificationRequest request) =>
        _verificationResult ?? new PaymentGatewayVerificationResult(
            PaymentGatewayVerificationOutcome.Succeeded,
            request.ProviderReference ?? $"verify-{request.PaymentAttemptId:N}",
            request.ExpectedAmount,
            request.ExpectedCurrency,
            null);

    internal string ReferenceFor(PaymentGatewayCreationRequest request) =>
        CreationProviderReference ?? $"ref-{request.PaymentAttemptId:N}";
}

public sealed class RecordingPaymentGateway(PaymentGatewayTestControl control) : IPaymentGateway
{
    public string ProviderName => "Recording";

    public Task<PaymentGatewayCreationResult> CreatePaymentAsync(
        PaymentGatewayCreationRequest request,
        CancellationToken cancellationToken)
    {
        control.Record(request);
        return control.NextCreation() switch
        {
            PaymentCreationBehavior.Pending => Task.FromResult(new PaymentGatewayCreationResult(
                PaymentGatewayCreationOutcome.PendingVerification,
                control.ReferenceFor(request),
                $"https://payments.test/{request.PaymentAttemptId:N}",
                null)),
            PaymentCreationBehavior.Failed => Task.FromResult(new PaymentGatewayCreationResult(
                PaymentGatewayCreationOutcome.Failed,
                null,
                null,
                "PAYMENT_PROVIDER_DECLINED")),
            PaymentCreationBehavior.Timeout => Task.FromException<PaymentGatewayCreationResult>(
                new TimeoutException("شبیه‌سازی timeout درگاه تست.")),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
        PaymentGatewayVerificationRequest request,
        CancellationToken cancellationToken)
    {
        control.Record(request);
        return Task.FromResult(control.VerificationResult(request));
    }
}
