using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using MakanApp.Infrastructure.Ai;
using Microsoft.Extensions.Options;

namespace MakanApp.Infrastructure.Knowledge;

public sealed class AiKnowledgeOptions
{
    public const string SectionName = "AiKnowledge";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ContractVersion { get; set; } = "knowledge-v1";
    public int TimeoutSeconds { get; set; } = 35;
    public int MaximumContentBytes { get; set; } = 10 * 1024 * 1024;
    public int MaximumResponseBytes { get; set; } = 2 * 1024 * 1024;
    public int StatusPollAttempts { get; set; } = 20;
    public int StatusPollIntervalMilliseconds { get; set; } = 500;
    public int StatusPollTimeoutSeconds { get; set; } = 60;
    public bool UseServiceAuthentication { get; set; }

    public static bool IsValid(AiKnowledgeOptions options) => !options.Enabled ||
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        options.ContractVersion == "knowledge-v1" && options.TimeoutSeconds is >= 1 and <= 180 &&
        options.MaximumContentBytes is >= 1 and <= 10 * 1024 * 1024 &&
        options.MaximumResponseBytes is >= 1024 and <= 4 * 1024 * 1024 &&
        options.StatusPollAttempts is >= 1 and <= 120 && options.StatusPollIntervalMilliseconds is >= 1 and <= 5000 &&
        options.StatusPollTimeoutSeconds is >= 1 and <= 600;
}

public sealed class KnowledgeRuntimeAvailability(IOptions<AiKnowledgeOptions> knowledge, AiServiceOptions business)
    : IKnowledgeRuntimeAvailability
{
    public void EnsureEnabled()
    {
        if (!knowledge.Value.Enabled || !business.Enabled)
            throw new AiServiceException(AiServiceErrorCodes.FeatureDisabled, "قابلیت دانش فعال نیست.");
    }
}
