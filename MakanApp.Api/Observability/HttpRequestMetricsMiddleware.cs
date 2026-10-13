using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace MakanApp.Api.Observability;

public sealed class HttpRequestMetricsMiddleware(
    RequestDelegate next,
    MakanApiMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var route = context.GetEndpoint() is RouteEndpoint endpoint
                ? endpoint.RoutePattern.RawText ?? "unknown"
                : "unmatched";
            var statusClass = $"{context.Response.StatusCode / 100}xx";
            var tags = new TagList
            {
                { "http.request.method", context.Request.Method },
                { "http.route", route },
                { "http.response.status_class", statusClass }
            };
            metrics.RequestCount.Add(1, tags);
            if (context.Response.StatusCode >= 500)
            {
                metrics.RequestErrors.Add(1, tags);
            }

            metrics.RequestDuration.Record(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                tags);
        }
    }
}
