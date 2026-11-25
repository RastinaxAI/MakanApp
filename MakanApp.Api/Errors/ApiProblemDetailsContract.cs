using System.Globalization;
using MakanApp.Application.Ai;
using MakanApp.Application.Knowledge;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Errors;

public static class ApiProblemDetailsContract
{
    public static void AddFrameworkCode(ProblemDetailsContext context)
    {
        // کد اختصاصی دامنه، حتی در 404 پنهان‌کنندهٔ منبع، هرگز جایگزین نمی‌شود.
        if (context.ProblemDetails.Extensions.ContainsKey("code")) return;
        var code = FrameworkCode(context.ProblemDetails.Status);
        if (code is not null) context.ProblemDetails.Extensions.Add("code", code);
    }

    public static async Task WriteEmptyErrorAsync(StatusCodeContext statusContext)
    {
        var context = statusContext.HttpContext;
        // صفحهٔ وب و HEAD قرارداد بدنهٔ API ندارند؛ پاسخ نوشته‌شده را خود middleware دست نمی‌زند.
        if (!context.Request.Path.StartsWithSegments("/api") || HttpMethods.IsHead(context.Request.Method)) return;
        var code = FrameworkCode(context.Response.StatusCode);
        if (code is null) return;
        var problem = new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = "Request failed",
            Detail = context.Response.StatusCode switch
            {
                404 => "مسیر درخواستی در دسترس نیست.",
                405 => "روش این درخواست برای مسیر انتخاب‌شده مجاز نیست.",
                413 => "اندازهٔ درخواست بیش از حد مجاز است.",
                415 => "نوع محتوای ارسال‌شده مجاز نیست.",
                _ => "اطلاعات درخواست معتبر نیست."
            },
            Type = $"urn:makan:problem:{code.ToLowerInvariant().Replace('_', '-')}",
            Extensions = { ["code"] = code }
        };
        var writer = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        if (await writer.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem })) return;
        // Accept نامناسب نباید business code و شناسهٔ پیگیری پاسخ خطا را حذف کند.
        problem.Extensions.TryAdd("traceId", System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier);
        problem.Extensions.TryAdd("correlationId", context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(problem, options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }

    public static void PreserveRetryAfter(HttpContext context, Exception exception)
    {
        if (context.Response.StatusCode is not (429 or 503)) return;
        var delay = exception switch
        {
            AiServiceException ai => ai.RetryAfter,
            AiKnowledgeException knowledge => knowledge.RetryAfter,
            _ => null
        };
        if (delay is null || delay < TimeSpan.Zero) return;
        // تبدیل به عدد صحیح رو به بالا، بدون انتقال header خام یا کوتاه‌کردن انتظار provider.
        context.Response.Headers.TryAdd("Retry-After",
            Math.Ceiling(delay.Value.TotalSeconds).ToString("0", CultureInfo.InvariantCulture));
    }

    private static string? FrameworkCode(int? status) => status switch
    {
        400 => ApiErrorCodes.InvalidRequest,
        404 => ApiErrorCodes.ResourceNotFound,
        405 => ApiErrorCodes.MethodNotAllowed,
        413 => ApiErrorCodes.RequestTooLarge,
        415 => ApiErrorCodes.UnsupportedMediaType,
        _ => null
    };
}
