using System.Net.Http.Headers;
using MakanApp.Application.Identity;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Identity.Sms;

public sealed class MeliPayamakSmsSender(
    HttpClient httpClient,
    MeliPayamakOptions options,
    ILogger<MeliPayamakSmsSender> logger) : ISmsSender
{
    private const string ProviderName = "MeliPayamak";
    private const string SendOtpPath = "api/SendSMS/SendOtp";

    public async Task SendOtpAsync(
        string normalizedPhoneNumber,
        Guid challengeId,
        string code,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        _ = challengeId;
        _ = expiresAtUtc;
        var recipient = ToProviderRecipient(normalizedPhoneNumber);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SendOtpPath)
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("username", options.Username),
                    new KeyValuePair<string, string>("password", options.Credential),
                    new KeyValuePair<string, string>("from", options.From),
                    new KeyValuePair<string, string>("to", recipient),
                    new KeyValuePair<string, string>("code", code)
                ])
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(
                "application/x-www-form-urlencoded");

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                LogFailure(recipient, SmsProviderFailureCategory.Unavailable);
                throw CreateException(SmsProviderFailureCategory.Unavailable);
            }

            var content = await response.Content.ReadAsStringAsync(timeoutSource.Token);
            var providerResponse = MeliPayamakProviderResponse.Parse(content);
            if (!providerResponse.IsSuccess)
            {
                var category = providerResponse.FailureCategory
                    ?? SmsProviderFailureCategory.Unavailable;
                LogFailure(recipient, category);
                throw CreateException(category);
            }

            logger.LogInformation(
                "ارسال OTP توسط {Provider} برای گیرنده {MaskedRecipient} با شناسه {ProviderMessageId} پذیرفته شد.",
                ProviderName,
                MaskRecipient(recipient),
                providerResponse.ProviderMessageId);
        }
        catch (IdentityException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogFailure(recipient, SmsProviderFailureCategory.Unavailable);
            throw CreateException(SmsProviderFailureCategory.Unavailable);
        }
        catch (HttpRequestException)
        {
            LogFailure(recipient, SmsProviderFailureCategory.Unavailable);
            throw CreateException(SmsProviderFailureCategory.Unavailable);
        }
    }

    private void LogFailure(string recipient, SmsProviderFailureCategory category) =>
        logger.LogWarning(
            "ارسال OTP توسط {Provider} برای گیرنده {MaskedRecipient} با دسته خطای {FailureCategory} ناموفق بود.",
            ProviderName,
            MaskRecipient(recipient),
            category);

    private static IdentityException CreateException(SmsProviderFailureCategory category) =>
        category switch
        {
            SmsProviderFailureCategory.Authentication => new IdentityException(
                IdentityErrorCodes.SmsProviderAuthenticationFailed,
                "سرویس ارسال پیامک احراز هویت نشد."),
            SmsProviderFailureCategory.Configuration => new IdentityException(
                IdentityErrorCodes.SmsProviderConfigurationError,
                "سرویس ارسال پیامک به پیکربندی معتبر نیاز دارد."),
            SmsProviderFailureCategory.RecipientInvalid => new IdentityException(
                IdentityErrorCodes.SmsRecipientInvalid,
                "شماره گیرنده برای ارسال پیامک معتبر نیست."),
            SmsProviderFailureCategory.RecipientBlocked => new IdentityException(
                IdentityErrorCodes.SmsRecipientBlocked,
                "ارسال پیامک به این گیرنده توسط سرویس‌دهنده پذیرفته نشد."),
            SmsProviderFailureCategory.RateLimited => new IdentityException(
                IdentityErrorCodes.SmsRateLimited,
                "محدودیت ارسال سرویس پیامک فعال است؛ بعداً دوباره تلاش کنید."),
            SmsProviderFailureCategory.Rejected => new IdentityException(
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
}
