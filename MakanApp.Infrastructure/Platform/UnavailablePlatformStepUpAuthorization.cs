using MakanApp.Application.Platform;

namespace MakanApp.Infrastructure.Platform;

public sealed class UnavailablePlatformStepUpAuthorization : IPlatformStepUpAuthorization
{
    public Task<bool> IsSatisfiedAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);
}
