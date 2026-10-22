namespace MakanApp.Infrastructure.Ai;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    // بودجهٔ محلی ۳۵ ثانیه‌ای حفظ می‌شود؛ تنظیم محیط می‌تواند آن را صریحاً تغییر دهد.
    public const int DefaultTimeoutSeconds = 35;
    public const int MaximumTimeoutSeconds = 180;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;
    public string ContractVersion { get; init; } = "v1";
    public int CircuitBreakerFailureThreshold { get; init; } = 3;
    public int CircuitBreakerBreakSeconds { get; init; } = 30;
}
