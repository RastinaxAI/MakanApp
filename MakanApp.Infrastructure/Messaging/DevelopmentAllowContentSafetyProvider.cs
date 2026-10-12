using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class DevelopmentAllowContentSafetyProvider : IContentSafetyProvider
{
    public Task<ContentSafetyResult> EvaluateAsync(
        ContentSafetyRequest request,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ContentSafetyResult(ContentSafetyDecision.Allowed));
}
