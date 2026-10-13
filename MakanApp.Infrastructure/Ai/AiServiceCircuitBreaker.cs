using MakanApp.Application.Ai;

namespace MakanApp.Infrastructure.Ai;

public sealed class AiServiceCircuitBreaker(
    AiServiceOptions options,
    TimeProvider timeProvider)
{
    private readonly object _sync = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openUntilUtc;

    public void ThrowIfOpen()
    {
        lock (_sync)
        {
            if (!_openUntilUtc.HasValue)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            if (now < _openUntilUtc.Value)
            {
                throw new AiServiceException(
                    AiServiceErrorCodes.ServiceUnavailable,
                    "سرویس هوش مصنوعی موقتاً در دسترس نیست.");
            }

            _openUntilUtc = null;
            _consecutiveFailures = 0;
        }
    }

    public void RecordSuccess()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _openUntilUtc = null;
        }
    }

    public void RecordTransientFailure()
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= options.CircuitBreakerFailureThreshold)
            {
                _openUntilUtc = timeProvider.GetUtcNow()
                    .AddSeconds(options.CircuitBreakerBreakSeconds);
            }
        }
    }
}
