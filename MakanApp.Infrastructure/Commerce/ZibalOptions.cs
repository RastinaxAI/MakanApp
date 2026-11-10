using Microsoft.Extensions.Options;

namespace MakanApp.Infrastructure.Commerce;

public sealed class ZibalOptions
{
    public const string SectionName = "Payments:Zibal";
    public const string OfficialOrigin = "https://gateway.zibal.ir";
    public const string CallbackPath = "/api/integrations/v1/commerce/payments/zibal/callback";
    public string BaseUrl { get; init; } = OfficialOrigin;
    public string Merchant { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = string.Empty;
    public string ClientReturnUrl { get; init; } = string.Empty;
    public string[] AllowedClientReturnOrigins { get; init; } = [];
    public int TimeoutSeconds { get; init; } = 15;

    public bool IsComplete => !string.IsNullOrWhiteSpace(Merchant) && IsHttpUrl(CallbackUrl, out _)
        && IsAllowedReturn(ClientReturnUrl);

    public void Validate(string environmentName, bool selected)
    {
        var test = environmentName is "Test" or "Testing";
        var production = environmentName is "Production" or "Staging";
        if (string.Equals(Merchant, "zibal", StringComparison.OrdinalIgnoreCase) && !test) Fail();
        if (!selected && string.IsNullOrWhiteSpace(Merchant)) return;
        if (BaseUrl?.TrimEnd('/') != OfficialOrigin || TimeoutSeconds is < 5 or > 30 || !IsComplete
            || Merchant.Length > 200 || Merchant.Any(char.IsWhiteSpace)
            || !IsHttpUrl(CallbackUrl, out var callback) || callback.AbsolutePath != CallbackPath
            || !string.IsNullOrEmpty(callback.Query) || !string.IsNullOrEmpty(callback.Fragment)
            || production && (callback.Scheme != "https" || new Uri(ClientReturnUrl).Scheme != "https")
            || test && Merchant != "zibal") Fail();
    }

    public bool IsAllowedReturn(string value) => IsHttpUrl(value, out var uri)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (AllowedClientReturnOrigins ?? []).Any(origin => IsHttpUrl(origin, out var allowed)
            && allowed.AbsolutePath == "/" && string.IsNullOrEmpty(allowed.Query) && string.IsNullOrEmpty(allowed.Fragment)
            && uri.GetLeftPart(UriPartial.Authority).Equals(allowed.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase));

    private static bool IsHttpUrl(string? value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) && parsed.Scheme is "https" or "http"
            && string.IsNullOrEmpty(parsed.UserInfo)) { uri = parsed; return true; }
        uri = null!; return false;
    }

    private static void Fail() => throw new OptionsValidationException(SectionName, typeof(ZibalOptions),
        ["تنظیمات زیبال معتبر نیست؛ Merchant، محیط، HTTPS، Callback، Return allowlist و timeout را در تنظیمات خصوصی بررسی کنید."]);
}
