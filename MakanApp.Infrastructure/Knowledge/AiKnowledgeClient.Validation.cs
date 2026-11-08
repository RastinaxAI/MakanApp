using MakanApp.Application.Knowledge;

namespace MakanApp.Infrastructure.Knowledge;

public sealed partial class AiKnowledgeClient
{
    private static readonly HashSet<string> States = Enum.GetNames<KnowledgeProcessingState>().ToHashSet(StringComparer.Ordinal);
    private static bool ValidIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value == value.Trim() && !value.Any(char.IsControl);
    private static void ValidateEnvelope(string requestId, string correlationId)
    { if (!ValidIdentifier(requestId) || !ValidIdentifier(correlationId)) throw InvalidRequest(); }
    private static void ValidateIdentity(KnowledgeSourceVersion? source)
    { if (source is null || source.SourceId == Guid.Empty || source.Version <= 0) throw InvalidRequest(); }
    private static bool ValidScope(KnowledgeScope? scope) => scope is not null && scope.Type switch
    {
        "Personal" => scope.OwnerUserId is Guid owner && owner != Guid.Empty && scope.OrganizationId is null && scope.ClassId is null,
        "Class" => scope.OwnerUserId is null && scope.OrganizationId is Guid org && org != Guid.Empty && scope.ClassId is Guid cls && cls != Guid.Empty,
        "Organization" => scope.OwnerUserId is null && scope.OrganizationId is Guid org && org != Guid.Empty && scope.ClassId is null,
        _ => false
    };
    private static void ValidateStatus(KnowledgeSourceVersion identity, KnowledgeProcessingStatus status)
    {
        if (status.SourceId != identity.SourceId || status.Version != identity.Version || !States.Contains(status.State) ||
            status.UpdatedAtUtc == default || status.UpdatedAtUtc.Offset != TimeSpan.Zero || status.ReadyAtUtc?.Offset != TimeSpan.Zero && status.ReadyAtUtc.HasValue ||
            status.FailureCode is not (null or "AI_OCR_FAILED") || status.FailureCode is not null && status.State != "OcrFailed")
            throw InvalidResponse();
    }
    private static void ValidateQuery(string requestId, string correlationId, string query,
        IReadOnlyList<KnowledgeSourceVersion>? sources, int maximum)
    {
        ValidateEnvelope(requestId, correlationId);
        if (string.IsNullOrWhiteSpace(query) || query.Length > 4000 || maximum is < 1 or > 20 || sources is null ||
            sources.Count is < 1 or > 32 || sources.Any(x => x is null || x.SourceId == Guid.Empty || x.Version < 1) ||
            sources.Distinct().Count() != sources.Count) throw InvalidRequest();
    }
    private static bool ValidCitation(KnowledgeCitation? citation) => citation is not null && citation.SourceId != Guid.Empty &&
        citation.Version > 0 && ValidIdentifier(citation.ChunkId) && !string.IsNullOrWhiteSpace(citation.Title) &&
        citation.PageStart.HasValue == citation.PageEnd.HasValue &&
        (!citation.PageStart.HasValue || citation.PageStart > 0 && citation.PageEnd >= citation.PageStart) &&
        (citation.SectionTitle is null || !string.IsNullOrWhiteSpace(citation.SectionTitle)) && citation.QuotePreview?.Length is not > 300;

    private static void ValidateRetrieval(KnowledgeRetrieveRequest request, KnowledgeRetrieveResponse result)
    {
        if (result.RequestId != request.RequestId || result.CorrelationId != request.CorrelationId || result.Results is null ||
            result.Results.Count > request.MaxResults || result.Status is not ("Ok" or "Empty") ||
            (result.Status == "Empty") != (result.Results.Count == 0)) throw InvalidResponse();
        var keys = new HashSet<(Guid, int, string)>();
        for (var i = 0; i < result.Results.Count; i++)
        {
            var item = result.Results[i];
            if (item is null || item.Rank != i + 1 || !request.AllowedSourceVersions.Contains(new(item.SourceId, item.Version)) ||
                !ValidIdentifier(item.ChunkId) || string.IsNullOrWhiteSpace(item.Text) || !ValidCitation(item.Citation) ||
                item.Citation.SourceId != item.SourceId || item.Citation.Version != item.Version || item.Citation.ChunkId != item.ChunkId ||
                !keys.Add((item.SourceId, item.Version, item.ChunkId)) ||
                !request.IncludeQuotePreview && item.Citation.QuotePreview is not null ||
                item.Citation.QuotePreview is string preview && !item.Text.Contains(preview, StringComparison.Ordinal)) throw InvalidResponse();
        }
    }

    private static void ValidateAnswer(KnowledgeRagRequest request, KnowledgeRetrieveResponse evidence, KnowledgeRagResponse result)
    {
        if (result.RequestId != request.RequestId || result.CorrelationId != request.CorrelationId || result.Claims is null ||
            result.Citations is null || string.IsNullOrWhiteSpace(result.Answer) || result.Status is not ("Ok" or "InsufficientEvidence") ||
            result.Claims.Count > 12 || result.Citations.Count > 20) throw InvalidResponse();
        if (result.Status == "InsufficientEvidence")
        {
            if (result.Claims.Count != 0 || result.Citations.Count != 0) throw InvalidResponse();
            return;
        }
        if (result.Claims.Count == 0 || result.Answer != string.Join("\n", result.Claims.Select(c => c?.Text))) throw InvalidResponse();
        var ordered = new List<KnowledgeCitation>();
        var seen = new HashSet<(Guid, int, string)>();
        foreach (var claim in result.Claims)
        {
            if (claim is null || string.IsNullOrWhiteSpace(claim.Text) || claim.Text.Length > 2000 || claim.Citations is null ||
                claim.Citations.Count is < 1 or > 20 || claim.Citations.DistinctBy(c => (c?.SourceId, c?.Version, c?.ChunkId)).Count() != claim.Citations.Count)
                throw InvalidResponse();
            foreach (var citation in claim.Citations)
            {
                if (!ValidCitation(citation) || !evidence.Results.Any(row => row.Citation == citation)) throw InvalidResponse();
                if (seen.Add((citation.SourceId, citation.Version, citation.ChunkId))) ordered.Add(citation);
            }
        }
        if (!ordered.SequenceEqual(result.Citations)) throw InvalidResponse();
    }
}
