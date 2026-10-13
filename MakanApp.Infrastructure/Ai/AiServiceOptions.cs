namespace MakanApp.Infrastructure.Ai;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "http://localhost:8000";
    public int TimeoutSeconds { get; init; } = 15;
    public string ContractVersion { get; init; } = "v1";
    public int CircuitBreakerFailureThreshold { get; init; } = 3;
    public int CircuitBreakerBreakSeconds { get; init; } = 30;
}
