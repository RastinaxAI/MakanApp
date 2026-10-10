using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Identity.Otp.MeliPayamak;

public sealed class MeliPayamakOtpProvider(
    HttpClient httpClient,
    MeliPayamakOtpOptions options,
    ILogger<MeliPayamakOtpProvider> logger) : IOtpDeliveryProvider
{
    private const string ProviderName = "MeliPayamakOtp";

    public async Task<OtpDeliveryResult> RequestOtpAsync(
        string normalizedPhoneNumber,
        CancellationToken cancellationToken)
    {
        var recipient = ToProviderRecipient(normalizedPhoneNumber);
        var endpoint = $"api/send/otp/{Uri.EscapeDataString(options.ServiceToken)}";
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new MeliPayamakOtpRequest(recipient))
            };

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                var category = MapHttpFailure(response.StatusCode);
                LogFailure(recipient, category);
                throw CreateException(category);
            }

            var content = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            if (!MeliPayamakOtpResponse.TryParse(content, out var providerResponse))
            {
                LogFailure(recipient, OtpProviderFailureCategory.Unavailable);
                throw CreateException(OtpProviderFailureCategory.Unavailable);
            }

            logger.LogInformation(
                "درخواست OTP توسط {Provider} برای گیرنده {MaskedRecipient} پذیرفته شد.",
                ProviderName,
                MaskRecipient(recipient));
            return new OtpDeliveryResult(
                providerResponse!.Code,
                providerResponse.Status);
        }
        catch (IdentityException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure(recipient, OtpProviderFailureCategory.Unavailable);
            throw CreateException(OtpProviderFailureCategory.Unavailable);
        }
        catch (HttpRequestException)
        {
            LogFailure(recipient, OtpProviderFailureCategory.Unavailable);
            throw CreateException(OtpProviderFailureCategory.Unavailable);
        }
    }

    private void LogFailure(string recipient, OtpProviderFailureCategory category) =>
        logger.LogWarning(
            "درخواست OTP توسط {Provider} برای گیرنده {MaskedRecipient} با دسته خطای {FailureCategory} ناموفق بود.",
            ProviderName,
            MaskRecipient(recipient),
            category);

    private static OtpProviderFailureCategory MapHttpFailure(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                OtpProviderFailureCategory.Authentication,
            HttpStatusCode.TooManyRequests => OtpProviderFailureCategory.RateLimited,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                OtpProviderFailureCategory.Rejected,
            _ => OtpProviderFailureCategory.Unavailable
        };

    private static IdentityException CreateException(OtpProviderFailureCategory category) =>
        category switch
        {
            OtpProviderFailureCategory.Authentication => new IdentityException(
                IdentityErrorCodes.SmsProviderAuthenticationFailed,
                "سرویس ارسال پیامک احراز هویت نشد."),
            OtpProviderFailureCategory.RateLimited => new IdentityException(
                IdentityErrorCodes.SmsRateLimited,
                "محدودیت ارسال سرویس پیامک فعال است؛ بعداً دوباره تلاش کنید."),
            OtpProviderFailureCategory.Rejected => new IdentityException(
                IdentityErrorCodes.SmsProviderRejected,
                "درخواست ارسال پیامک توسط سرویس‌دهنده پذیرفته نشد."),
            _ => new IdentityException(
                IdentityErrorCodes.SmsProviderUnavailable,
                "سرویس ارسال پیامک موقتاً در دسترس نیست.")
        };

    private static string ToProviderRecipient(string normalizedPhoneNumber) =>
        normalizedPhoneNumber.Length == 13 &&
        normalizedPhoneNumber.StartsWith("+989", StringComparison.Ordinal)
            ? $"0{normalizedPhoneNumber[3..]}"
            : normalizedPhoneNumber;

    private static string MaskRecipient(string recipient) =>
        recipient.Length <= 4 ? "****" : $"*******{recipient[^4..]}";

    private sealed record MeliPayamakOtpRequest(
        [property: JsonPropertyName("to")] string To);

    private enum OtpProviderFailureCategory
    {
        Authentication,
        Rejected,
        Unavailable,
        RateLimited
    }
}
