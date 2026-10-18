using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace MakanApp.Api.Observability;

public sealed class HttpRequestMetricsMiddleware(
    RequestDelegate next,
    MakanApiMetrics metrics,
    ILogger<HttpRequestMetricsMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = "completed";
        try
        {
            await next(context);
            if (context.RequestAborted.IsCancellationRequested) outcome = "cancelled";
        }
        catch (Exception) when (context.RequestAborted.IsCancellationRequested)
        {
            outcome = "cancelled";
            // token و معنای cancellation حفظ می‌شود، نه متن احتمالی exception اصلی.
            throw new OperationCanceledException("HTTP_REQUEST_CANCELLED", context.RequestAborted);
        }
        catch (Exception)
        {
            outcome = context.Response.HasStarted ? "aborted" : "unhandled";
            logger.LogError(new EventId(1002, "RequestPipelineFailure"),
                "خطای امن {ErrorCode} از مرز HTTP عبور کرد. CorrelationId: {CorrelationId}",
                "HTTP_PIPELINE_FAILED", context.TraceIdentifier);
            // خطا بلعیده نمی‌شود و status ارسال‌شده تغییر نمی‌کند؛ host فقط failure امن را می‌بیند.
            throw new SanitizedRequestFailureException();
        }
        finally
        {
            var endpoint = context.GetEndpoint() as RouteEndpoint ??
                context.Features.Get<IExceptionHandlerFeature>()?.Endpoint as RouteEndpoint;
            var route = endpoint is not null
                ? endpoint.RoutePattern.RawText ?? "unknown"
                : "unmatched";
            var method = NormalizeMethod(context.Request.Method);
            var statusClass = $"{context.Response.StatusCode / 100}xx";
            var tags = new TagList
            {
                { "http.request.method", method },
                { "http.route", route },
                { "http.response.status_class", statusClass }
            };
            metrics.RequestCount.Add(1, tags);
            if (context.Response.StatusCode >= 500)
            {
                metrics.RequestErrors.Add(1, tags);
            }

            if (outcome != "completed")
            {
                var failureTags = tags;
                failureTags.Add("outcome", outcome);
                metrics.RequestFailures.Add(1, failureTags);
            }

            var duration = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            metrics.RequestDuration.Record(duration, tags);
            logger.LogInformation(new EventId(1000, "RequestCompleted"),
                "درخواست {Method} {Route} با {StatusCode} در {DurationMs}ms پایان یافت. Outcome: {Outcome}; CorrelationId: {CorrelationId}; TraceId: {TraceId}",
                method, route, context.Response.StatusCode, duration, outcome, context.TraceIdentifier,
                Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
        }
    }

    private static string NormalizeMethod(string method) => method switch
    {
        "GET" or "HEAD" or "POST" or "PUT" or "DELETE" or "CONNECT" or "OPTIONS" or "TRACE" or "PATCH" => method,
        _ => "OTHER"
    };
}
