using System.Text.Json;

namespace MakanApp.Infrastructure.Identity.Otp.MeliPayamak;

internal sealed record MeliPayamakOtpResponse(
    string Code,
    string? Status)
{
    public static bool TryParse(string? content, out MeliPayamakOtpResponse? response)
    {
        response = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryGetProperty(document.RootElement, "code", out var codeElement) ||
                codeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var code = codeElement.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            string? status = null;
            if (TryGetProperty(document.RootElement, "status", out var statusElement) &&
                statusElement.ValueKind == JsonValueKind.String)
            {
                status = statusElement.GetString()?.Trim();
            }

            response = new MeliPayamakOtpResponse(code, status);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
