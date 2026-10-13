using MakanApp.Application.Identity;
using Microsoft.Extensions.Logging;

namespace MakanApp.Infrastructure.Identity;

public sealed class LoggingIdentitySecurityEventRecorder(
    ILogger<LoggingIdentitySecurityEventRecorder> logger) : IIdentitySecurityEventRecorder
{
    public Task RecordRefreshTokenReuseAsync(
        Guid userId,
        Guid sessionId,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "بازاستفاده refresh token شناسایی و نشست لغو شد. UserId: {UserId}, SessionId: {SessionId}, OccurredAtUtc: {OccurredAtUtc}",
            userId,
            sessionId,
            occurredAtUtc);
        return Task.CompletedTask;
    }
}
