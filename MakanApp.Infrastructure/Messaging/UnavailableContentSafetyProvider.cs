using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class UnavailableContentSafetyProvider : IContentSafetyProvider
{
    public Task<ContentSafetyResult> EvaluateAsync(
        ContentSafetyRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ContentSafetyResult(ContentSafetyDecision.Unavailable,
            ReasonCode: "ProviderUnavailable", PolicyVersion: "makan-child-safety-v1-unavailable",
            CorrelationId: Guid.NewGuid().ToString("N")));
}
