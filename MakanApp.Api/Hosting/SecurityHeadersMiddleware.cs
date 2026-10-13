namespace MakanApp.Api.Hosting;

public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Frame-Options"] = "DENY";
            if (environment.IsProduction() &&
                !context.Request.Path.StartsWithSegments("/swagger") &&
                !context.Request.Path.StartsWithSegments("/openapi"))
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
            }
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            return Task.CompletedTask;
        });
        await next(context);
    }
}
