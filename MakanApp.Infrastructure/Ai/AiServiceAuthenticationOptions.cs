namespace MakanApp.Infrastructure.Ai;

public sealed class AiServiceAuthenticationOptions
{
    public const string SectionName = "AiService:Authentication";

    public string Issuer { get; init; } = "makan-backend";
    public string Audience { get; init; } = "makan-ai";
    public string ServiceName { get; init; } = "makan-backend";
    public string Scope { get; init; } = "makan.ai.invoke";
    public string CurrentKeyId { get; init; } = string.Empty;
    public string CurrentKey { get; init; } = string.Empty;
    public int TokenLifetimeSeconds { get; init; } = 120;
}
