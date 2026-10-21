using System.Collections.ObjectModel;
using MakanApp.Application.Identity;
using Microsoft.Extensions.Configuration;

namespace MakanApp.Infrastructure.Identity;

public sealed class DevelopmentOtpBypassOptions
{
    public OtpMode Mode { get; private init; }
    public string DevelopmentCode { get; private init; } = string.Empty;
    public IReadOnlyDictionary<string, Guid> TestUsers { get; private init; } =
        new ReadOnlyDictionary<string, Guid>(new Dictionary<string, Guid>());

    public static DevelopmentOtpBypassOptions Load(IConfiguration configuration, string environmentName)
    {
        var mode = configuration["Identity:Otp:Mode"]?.Trim() ?? "Real";
        if (mode.Equals("Real", StringComparison.OrdinalIgnoreCase)) return new();
        if (!mode.Equals("DevelopmentBypass", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Identity:Otp:Mode باید Real یا DevelopmentBypass باشد.");
        if (!new[] { "Development", "Testing", "Test" }.Contains(environmentName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("DevelopmentBypass فقط در محیط Development/Test/Testing مجاز است.");
        var code = configuration["Identity:Otp:DevelopmentCode"] ?? string.Empty;
        var minimum = configuration.GetValue("Identity:Otp:MinimumCodeLength", 4);
        var maximum = configuration.GetValue("Identity:Otp:MaximumCodeLength", 64);
        if (minimum < 1 || maximum < minimum || maximum > 256 ||
            code.Length < minimum || code.Length > maximum || code.Length > 256 ||
            code.Length == 0 || code.Any(character => character is < '0' or > '9'))
            throw new InvalidOperationException("DevelopmentCode باید رقم ASCII و مطابق محدودیت طول OTP باشد؛ مقدار آن محرمانه است.");
        var users = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var entry in configuration.GetSection("Identity:Otp:TestUsers").GetChildren())
        {
            if (!PhoneNumberNormalizer.TryNormalize(entry["PhoneNumber"], out var phone) ||
                !Guid.TryParse(entry["UserId"], out var userId) || userId == Guid.Empty ||
                !users.TryAdd(phone, userId) || users.Count > 100)
                throw new InvalidOperationException("TestUsers باید شامل شماره‌های یکتا و UserId صریح و معتبر باشد.");
        }
        if (users.Count == 0)
            throw new InvalidOperationException("DevelopmentBypass بدون TestUsers مجاز نیست.");
        return new()
        {
            Mode = OtpMode.DevelopmentBypass,
            DevelopmentCode = code,
            TestUsers = new ReadOnlyDictionary<string, Guid>(users)
        };
    }
}
