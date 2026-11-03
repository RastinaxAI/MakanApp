using System.Globalization;

namespace MakanApp.Api.OpenApi;

public sealed class OpenApiDocumentCultureMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase))
        { await next(context); return; }
        // writer نسخهٔ فعلی OpenAPI نباید جداکنندهٔ اعشاری culture میزبان را وارد JSON کند.
        var previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; await next(context); }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
