using System.Diagnostics;

namespace MakanApp.Api.Hosting;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var headerValues = context.Request.Headers[HeaderName];
        var supplied = headerValues.Count == 1 && headerValues[0] is { Length: <= 64 } single
            ? single.Trim()
            : string.Empty;
        var correlationId = IsSafe(supplied)
            ? supplied
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        context.TraceIdentifier = correlationId;
        Activity.Current?.SetTag("makan.correlation_id", correlationId);
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString() ?? correlationId
        });
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        await next(context);
    }

    private static bool IsSafe(string value) =>
        value.Length is > 0 and <= 64 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
}
