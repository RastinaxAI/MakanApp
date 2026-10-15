using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record ContentSafetyAttachment(
    Guid FileAssetId,
    string ContentType,
    long SizeBytes,
    string Sha256Hash);

public sealed record ContentSafetyRequest(
    Guid ConversationId,
    Guid ActorUserId,
    Guid ClientMessageId,
    MessageKind Kind,
    string? Text,
    IReadOnlyCollection<ContentSafetyAttachment> Attachments);

public sealed record ContentSafetyResult(
    ContentSafetyDecision Decision,
    string? RiskLevel = null,
    string? ProviderReference = null,
    IReadOnlyCollection<string>? PolicyCodes = null,
    string? ReasonCode = null,
    string? PolicyVersion = null,
    string? CorrelationId = null);

public interface IContentSafetyProvider
{
    Task<ContentSafetyResult> EvaluateAsync(
        ContentSafetyRequest request,
        CancellationToken cancellationToken);
}

public sealed class ContentSafetyProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
