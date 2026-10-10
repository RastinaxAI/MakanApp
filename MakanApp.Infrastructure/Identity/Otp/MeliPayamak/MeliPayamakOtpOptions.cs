namespace MakanApp.Infrastructure.Identity.Otp.MeliPayamak;

public sealed class MeliPayamakOtpOptions
{
    public const string SectionName = "Sms:MeliPayamakOtp";
    public const string DefaultBaseUrl = "https://console.melipayamak.com";
    public const int DefaultTimeoutSeconds = 10;

    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public string ServiceToken { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    public Uri ValidateAndGetBaseUri()
    {
        if (string.IsNullOrWhiteSpace(ServiceToken) ||
            ServiceToken.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                "Sms:MeliPayamakOtp:ServiceToken must be supplied by a secret store when MeliPayamakOtp is selected.");
        }

        if (TimeoutSeconds is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "Sms:MeliPayamakOtp:TimeoutSeconds must be between 1 and 60.");
        }

        if (!Uri.TryCreate(BaseUrl.TrimEnd('/') + '/', UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment) ||
            baseUri.AbsolutePath != "/")
        {
            throw new InvalidOperationException(
                "Sms:MeliPayamakOtp:BaseUrl must be an absolute HTTPS origin without credentials, path, query or fragment.");
        }

        return baseUri;
    }
}
