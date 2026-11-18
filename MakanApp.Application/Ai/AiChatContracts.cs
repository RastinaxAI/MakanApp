namespace MakanApp.Application.Ai;

public sealed record SendAiChatCommand(
    string Message,
    Guid? ConversationId)
{
    public IReadOnlyCollection<Guid>? SelectedSourceIds { get; init; }
    public Guid? OperationId { get; init; }
    public AiAssistantProfile? AssistantProfile { get; init; }
    public AiChatMode? Mode { get; init; }
    public AiSourceIntent? SourceIntent { get; init; }
    public IReadOnlyList<MakanApp.Application.Knowledge.KnowledgeSourceVersion>? SelectedSources { get; init; }
}

public sealed record AiChatResult(
    string RequestId,
    string? CorrelationId,
    string? ConversationId,
    AiMessage Answer,
    IReadOnlyCollection<AiInsight> Insights,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources)
{
    public AiChatProvenance Provenance { get; init; } = new(AiChatProvenanceKind.General);
    public IReadOnlyList<MakanApp.Application.Knowledge.KnowledgeCitation> Citations { get; init; } = [];
}
