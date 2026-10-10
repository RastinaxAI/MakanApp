namespace MakanApp.Infrastructure.Identity.Sms;

public sealed class MeliPayamakOptions
{
    public const string SectionName = "Sms:MeliPayamak";
    public const string DefaultBaseUrl = "https://rest.payamak-panel.com";
    public const int DefaultTimeoutSeconds = 10;

    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public string Username { get; init; } = string.Empty;
    public string Credential { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    public Uri ValidateAndGetBaseUri()
    {
        if (string.IsNullOrWhiteSpace(Username) ||
            string.IsNullOrWhiteSpace(Credential) ||
            string.IsNullOrWhiteSpace(From))
        {
            throw new InvalidOperationException(
                "Sms:MeliPayamak:Username, Credential and From must be supplied by a secret store when MeliPayamak is selected.");
        }

        if (TimeoutSeconds is < 1 or > 60)
        {
            throw new InvalidOperationException(
                "Sms:MeliPayamak:TimeoutSeconds must be between 1 and 60.");
        }

        if (!Uri.TryCreate(BaseUrl.TrimEnd('/') + '/', UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(baseUri.UserInfo) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new InvalidOperationException(
                "Sms:MeliPayamak:BaseUrl must be an absolute HTTPS URL without credentials, query or fragment.");
        }

        return baseUri;
    }
}
