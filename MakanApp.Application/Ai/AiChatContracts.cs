namespace MakanApp.Application.Ai;

public sealed record SendAiChatCommand(
    string Message,
    Guid? ConversationId);

public sealed record AiChatResult(
    string RequestId,
    string? CorrelationId,
    string? ConversationId,
    AiMessage Answer,
    IReadOnlyCollection<AiInsight> Insights,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources);
