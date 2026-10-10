using System.Globalization;
using System.Text.Json;

namespace MakanApp.Infrastructure.Identity.Sms;

internal enum SmsProviderFailureCategory
{
    Authentication,
    Configuration,
    Rejected,
    Unavailable,
    RecipientInvalid,
    RecipientBlocked,
    RateLimited
}

internal sealed record MeliPayamakProviderResponse(
    bool IsSuccess,
    string? ProviderMessageId,
    long? ProviderCode,
    SmsProviderFailureCategory? FailureCategory)
{
    private static readonly IReadOnlyDictionary<long, SmsProviderFailureCategory> ErrorCodes =
        new Dictionary<long, SmsProviderFailureCategory>
        {
            [-111] = SmsProviderFailureCategory.Configuration,
            [-110] = SmsProviderFailureCategory.Configuration,
            [-109] = SmsProviderFailureCategory.Configuration,
            [-108] = SmsProviderFailureCategory.Authentication,
            [0] = SmsProviderFailureCategory.Authentication,
            [2] = SmsProviderFailureCategory.Rejected,
            [3] = SmsProviderFailureCategory.RateLimited,
            [4] = SmsProviderFailureCategory.RateLimited,
            [5] = SmsProviderFailureCategory.Configuration,
            [6] = SmsProviderFailureCategory.Unavailable,
            [7] = SmsProviderFailureCategory.Rejected,
            [9] = SmsProviderFailureCategory.Configuration,
            [10] = SmsProviderFailureCategory.Authentication,
            [11] = SmsProviderFailureCategory.Rejected,
            [12] = SmsProviderFailureCategory.Configuration,
            [14] = SmsProviderFailureCategory.Rejected,
            [15] = SmsProviderFailureCategory.Rejected,
            [16] = SmsProviderFailureCategory.RecipientInvalid,
            [17] = SmsProviderFailureCategory.Rejected,
            [18] = SmsProviderFailureCategory.RecipientInvalid,
            [35] = SmsProviderFailureCategory.RecipientBlocked
        };

    public static MeliPayamakProviderResponse Parse(string? content)
    {
        if (!TryReadNumericValue(content, out var value))
        {
            return Failure(null, SmsProviderFailureCategory.Unavailable);
        }

        if (ErrorCodes.TryGetValue(value, out var category))
        {
            return Failure(value, category);
        }

        return value > 0
            ? new MeliPayamakProviderResponse(
                true,
                value.ToString(CultureInfo.InvariantCulture),
                null,
                null)
            : Failure(value, SmsProviderFailureCategory.Rejected);
    }

    private static MeliPayamakProviderResponse Failure(
        long? providerCode,
        SmsProviderFailureCategory category) =>
        new(false, null, providerCode, category);

    private static bool TryReadNumericValue(string? content, out long value)
    {
        value = default;
        var candidate = content?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        if (long.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(candidate);
            return TryReadJsonValue(document.RootElement, out value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadJsonValue(JsonElement element, out long value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String &&
            long.TryParse(
                element.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if ((property.Name.Equals("ReturnValue", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("SendOtpResult", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("Value", StringComparison.OrdinalIgnoreCase)) &&
                    TryReadJsonValue(property.Value, out value))
                {
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
