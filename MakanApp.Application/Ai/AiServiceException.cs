namespace MakanApp.Application.Ai;

public sealed class AiServiceException(
    string code,
    string message,
    int? upstreamStatusCode = null,
    TimeSpan? retryAfter = null,
    Exception? innerException = null,
    string? upstreamErrorCode = null) : Exception(message, innerException)
{
    public string Code { get; } = code;
    public int? UpstreamStatusCode { get; } = upstreamStatusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public string? UpstreamErrorCode { get; } = upstreamErrorCode;
}
