using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using MakanApp.Application.Ai;

namespace MakanApp.Api.OpenApi;

public sealed class AiInvocationOperationTransformer(AiUsageOptions aiUsage) : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? "";
        if (route.StartsWith("api/v1/ai/conversations/", StringComparison.Ordinal))
        {
            operation.Description = "پنجرهٔ تاریخچهٔ کاملِ خود مالک در Assistant Context جاری؛ حداکثر ۲۰ turn و ۱۲۰۰۰ کاراکتر. مالک/بافت نامنطبق 404؛ revoke دسترسی آینده را قطع می‌کند. هیچ مسیر Admin یا تاریخچهٔ Messenger ندارد.";
            return Task.CompletedTask;
        }
        if (!route.StartsWith("api/v1/ai/", StringComparison.Ordinal) || route == "api/v1/ai/sources/preflight" ||
            route.StartsWith("api/v1/ai/knowledge/", StringComparison.Ordinal)) return Task.CompletedTask;
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Responses ??= new OpenApiResponses();
        operation.Parameters.Add(new OpenApiParameter { Name = "X-AI-Operation-Id", In = ParameterLocation.Header, Required = false,
            Description = "شناسهٔ ثابت intent برای Retry؛ در صورت وجود OperationId در body باید یکسان باشد. حذف Header برای هر درخواست شناسهٔ تازه می‌سازد.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" } });
        operation.Parameters.Add(new OpenApiParameter { Name = "X-AI-Payer", In = ParameterLocation.Header, Required = false,
            Description = aiUsage.IsUnmetered
                ? "در Unmetered این Header اختیاری فقط از نظر syntax/enum بررسی می‌شود؛ Subscription، پرداخت، سهمیه یا انتخاب Payer لازم نیست و اثر تجاری ندارد. مجوز نشست، نقش، Context و Source مستقل باقی‌اند."
                : "Context از Assistant selection می‌آید: در Organization پیش‌فرض Organization و در Personal پیش‌فرض Individual است؛ FreeTrial صریح است. Payer مجوز داده را تغییر نمی‌دهد؛ Organization در Personal ممنوع و fallback خودکار بسته است.",
            Schema = new OpenApiSchema { Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString("Organization"), new OpenApiString("Individual"), new OpenApiString("FreeTrial") } } });
        operation.Responses.TryAdd("409", new OpenApiResponse { Description = (aiUsage.IsUnmetered ? "تعارض عملیات؛ " : "تعارض عملیات/سهمیه؛ ") +
            "AI_OPERATION_ALREADY_COMPLETED / IN_PROGRESS / OUTCOME_UNKNOWN / PREVIOUSLY_FAILED. پاسخ خصوصی AI cache نمی‌شود و Provider دوباره صدا زده نمی‌شود." });
        if (route is "api/v1/ai/chat" or "api/v1/ai/chat/stream")
        {
            operation.Description = (operation.Description ?? "") + " زمینهٔ Personal با Actor رسمی Personal و scope تهی به سرویس AI v1 ارسال می‌شود؛ نقش سازمانی لازم نیست. " +
                (aiUsage.IsUnmetered
                    ? "در Unmetered ورود و Context معتبر لازم است؛ Subscription/Entitlement تجاری، سهمیه، Trial روزانه و سقف محلی ai-chat اعمال نمی‌شوند. رکورد عملیات پایدار پیش از AI ثبت می‌شود و مصرف تجاری ندارد. "
                    : "Assistant مستقل از School Workspace است. Individual پیش‌فرض و FreeTrial صریح است؛ Organization در Personal مجاز نیست. رزرو معتبر پیش از تماس AI لازم است. ") +
                "Profile اختیاری فقط سبک پاسخ را تغییر می‌دهد. History از SQL متعلق به مالک و Context جاری ساخته می‌شود؛ history کلاینت مجوز یا حافظهٔ معتبر نیست. ConversationId پاسخ canonical است. Mode=General بدون Knowledge، Auto با SourceIntent صریح General مجاز به fallback شفاف، SourcesOnly بدون fallback است. selectedSources نسخهٔ دقیق و selectedSourceIds انتخاب اولیه را pin می‌کنند؛ تغییر نسخهٔ گفت‌وگوی grounded به گفت‌وگوی تازه نیاز دارد.";
            if (route.EndsWith("/stream", StringComparison.Ordinal))
            {
                operation.Description += " SSE واقعی: start یک‌بار، delta با sequence متوالی از ۱، done موفق بدون متن تکراری، error شکست نهایی. EOF بدون terminal شکست است؛ قطع Client به upstream می‌رسد. RAG جریان رسمی ندارد و AI_SOURCE_STREAMING_NOT_SUPPORTED نیاز به chat غیرجریانی را اعلام می‌کند. پس از آغاز HTTP، error event معیار شکست است حتی با HTTP 200.";
                operation.Responses["200"] = new OpenApiResponse { Description = "text/event-stream؛ موفقیت فقط با done معتبر است.", Content = new Dictionary<string, OpenApiMediaType> {
                    ["text/event-stream"] = new() { Schema = new OpenApiSchema { Type = "string", Description = "event: start|delta|done|error و data: JSON؛ پس از هر event flush می‌شود." } } } };
            }
        }
        foreach (var response in operation.Responses.Values)
        {
            response.Headers ??= new Dictionary<string, OpenApiHeader>();
            response.Headers.TryAdd("X-AI-Operation-Id", new OpenApiHeader { Description = "شناسهٔ intent پذیرفته‌شده؛ کلید پیگیری است، نه credential یا مجوز.", Schema = new OpenApiSchema { Type = "string", Format = "uuid" } });
        }
        return Task.CompletedTask;
    }
}
