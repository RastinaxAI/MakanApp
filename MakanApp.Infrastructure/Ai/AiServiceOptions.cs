namespace MakanApp.Infrastructure.Ai;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    // مهلت صف ۶۰ ثانیه و پردازش ۹۰ ثانیه است؛ زمان شبکه نیز در این بودجه قرار دارد.
    public const int DefaultTimeoutSeconds = 180;
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;
    public string ContractVersion { get; init; } = "v1";
    public int CircuitBreakerFailureThreshold { get; init; } = 3;
    public int CircuitBreakerBreakSeconds { get; init; } = 30;
}
