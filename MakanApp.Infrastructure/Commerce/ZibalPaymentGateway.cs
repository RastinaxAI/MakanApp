using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.Extensions.Options;

namespace MakanApp.Infrastructure.Commerce;

public sealed class ZibalPaymentGateway(HttpClient client, IOptions<ZibalOptions> options) : IPaymentGateway
{
    public const string Name = "Zibal";
    private readonly ZibalOptions _options = options.Value;
    public bool IsConfigured => _options.IsComplete;
    public string ProviderName => Name;

    public async Task<PaymentGatewayCreationResult> CreatePaymentAsync(PaymentGatewayCreationRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new(PaymentGatewayCreationOutcome.Unavailable, null, null, CommerceErrorCodes.PaymentProviderUnavailable);
        if (!ValidAmount(request.Amount, request.Currency))
            return new(PaymentGatewayCreationOutcome.Failed, null, null, CommerceErrorCodes.PaymentAmountMismatch);
        using var deadline = Deadline(cancellationToken);
        var reply = await PostAsync("/v1/request", new
        {
            merchant = _options.Merchant, amount = checked((long)request.Amount), callbackUrl = _options.CallbackUrl,
            description = $"MakanApp order {request.OrderId:N}", orderId = request.PaymentAttemptId.ToString("N")
        }, deadline.Token, cancellationToken);
        if (reply?.Result == 100 && reply.TrackId is > 0)
        {
            var reference = reply.TrackId.Value.ToString(CultureInfo.InvariantCulture);
            return new(PaymentGatewayCreationOutcome.PendingVerification, reference, $"{ZibalOptions.OfficialOrigin}/start/{reference}", null)
                { ProviderResult = 100 };
        }
        var rejected = reply?.Result is 102 or 103 or 104 or 105 or 106 or 113 or 115 or 140;
        return new(rejected ? PaymentGatewayCreationOutcome.Failed : PaymentGatewayCreationOutcome.Unknown, null, null,
            reply?.Result is 102 or 103 or 104 or 115 ? CommerceErrorCodes.PaymentProviderUnavailable :
            reply?.Result is 105 or 113 ? CommerceErrorCodes.PaymentAmountMismatch :
            reply?.Result is 106 or 140 ? CommerceErrorCodes.PaymentInvalid : CommerceErrorCodes.PaymentPendingVerification)
            { ProviderResult = reply?.Result };
    }

    public async Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(PaymentGatewayVerificationRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return new(PaymentGatewayVerificationOutcome.Unavailable, null, null, null, CommerceErrorCodes.PaymentProviderUnavailable);
        if (!TryTrackId(request.ProviderReference, out var trackId)) return Unknown(CommerceErrorCodes.PaymentReferenceInvalid);
        if (!ValidAmount(request.ExpectedAmount, request.ExpectedCurrency)) return Unknown(CommerceErrorCodes.PaymentAmountMismatch);
        using var deadline = Deadline(cancellationToken);
        var payload = new { merchant = _options.Merchant, trackId };
        var observations = new List<PaymentProviderObservation>();
        var verified = await PostAsync("/v1/verify", payload, deadline.Token, cancellationToken);
        observations.Add(new(false, verified?.Result, verified?.Status));
        if (verified?.Result == 100) return Interpret(verified, request, trackId, observations);

        // ۲۰۱ اثبات پرداخت نیست؛ پاسخ کامل Inquiry باید به همان تلاش و مبلغ تعلق داشته باشد.
        if (verified?.Result is 201 or 202 || verified is null)
        {
            var inquiry = await PostAsync("/v1/inquiry", payload, deadline.Token, cancellationToken);
            observations.Add(new(true, inquiry?.Result, inquiry?.Status));
            if (inquiry?.Result == 100) return Interpret(inquiry, request, trackId, observations);
        }
        return Unknown(verified?.Result is 102 or 103 or 104 or 115 ? CommerceErrorCodes.PaymentProviderUnavailable :
            verified?.Result == 203 ? CommerceErrorCodes.PaymentReferenceInvalid : CommerceErrorCodes.PaymentPendingVerification, observations);
    }

    private static PaymentGatewayVerificationResult Interpret(Reply reply, PaymentGatewayVerificationRequest request, long trackId,
        IReadOnlyList<PaymentProviderObservation> observations)
    {
        if (reply.TrackId.HasValue && reply.TrackId.Value != trackId)
            return Unknown(CommerceErrorCodes.PaymentReferenceInvalid, observations);
        if (reply.OrderId != request.PaymentAttemptId.ToString("N")) return Unknown(CommerceErrorCodes.PaymentReferenceInvalid, observations);
        if (reply.Amount != request.ExpectedAmount) return Unknown(CommerceErrorCodes.PaymentAmountMismatch, observations);
        var reference = trackId.ToString(CultureInfo.InvariantCulture);
        if (reply.Status == 1 && reply.RefNumber is > 0)
            return new(PaymentGatewayVerificationOutcome.Succeeded, reference, reply.Amount, CurrencyCode.Irr, null)
            {
                SettlementReference = reply.RefNumber.Value.ToString(CultureInfo.InvariantCulture),
                ProviderPaidAtUtc = ParseExplicitUtc(reply.PaidAt), Observations = observations
            };
        if (reply.Status is >= 3 and <= 12 or 15 or 16 or 18 or 21)
            return new(PaymentGatewayVerificationOutcome.Failed, reference, null, null,
                reply.Status == 3 ? "PAYMENT_CANCELLED" : "PAYMENT_PROVIDER_DECLINED") { Observations = observations };
        return Unknown(CommerceErrorCodes.PaymentPendingVerification, observations);
    }

    private async Task<Reply?> PostAsync<T>(string path, T payload, CancellationToken deadlineToken, CancellationToken callerToken)
    {
        try
        {
            // آدرس ثابت رسمی است و Redirect غیرفعال است؛ Merchant به مقصد دیگری ارسال نمی‌شود.
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ZibalOptions.OfficialOrigin + path))
                { Content = JsonContent.Create(payload) };
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadlineToken);
            if (!response.IsSuccessStatusCode) return null;
            await response.Content.LoadIntoBufferAsync(64 * 1024, deadlineToken);
            return await response.Content.ReadFromJsonAsync<Reply>(cancellationToken: deadlineToken);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested) { return null; }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (System.IO.IOException) { return null; }
    }

    private CancellationTokenSource Deadline(CancellationToken callerToken)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        return deadline;
    }
    private static bool ValidAmount(decimal amount, CurrencyCode currency) => currency == CurrencyCode.Irr
        && amount is >= 1000 and <= 4_000_000_000 && decimal.Truncate(amount) == amount;
    private static bool TryTrackId(string? reference, out long value) => long.TryParse(reference, NumberStyles.None,
        CultureInfo.InvariantCulture, out value) && value > 0 && value.ToString(CultureInfo.InvariantCulture) == reference;
    private static PaymentGatewayVerificationResult Unknown(string code, IReadOnlyList<PaymentProviderObservation>? observations = null) =>
        new(PaymentGatewayVerificationOutcome.Unknown, null, null, null, code) { Observations = observations ?? [] };
    private static DateTime? ParseExplicitUtc(string? value) => value is not null &&
        (value.EndsWith('Z') || value.LastIndexOf('+') > 9 || value.LastIndexOf('-') > 9) &&
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp) ? timestamp.UtcDateTime : null;

    private sealed class Reply
    {
        public int? Result { get; init; }
        public int? Status { get; init; }
        public long? TrackId { get; init; }
        public decimal? Amount { get; init; }
        public long? RefNumber { get; init; }
        public string? OrderId { get; init; }
        public string? PaidAt { get; init; }
    }
}
