using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class AiInvocationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? "";
        if (!route.StartsWith("api/v1/ai/", StringComparison.Ordinal) || route == "api/v1/ai/sources/preflight") return Task.CompletedTask;
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Responses ??= new OpenApiResponses();
        operation.Parameters.Add(new OpenApiParameter { Name = "X-AI-Operation-Id", In = ParameterLocation.Header, Required = false,
            Description = "شناسهٔ ثابت intent برای Retry؛ در صورت وجود OperationId در body باید یکسان باشد. حذف Header برای هر درخواست شناسهٔ تازه می‌سازد.",
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" } });
        operation.Parameters.Add(new OpenApiParameter { Name = "X-AI-Payer", In = ParameterLocation.Header, Required = false,
            Description = "پیش‌فرض سازمان جاری؛ Individual یا FreeTrial فقط با انتخاب صریح و بدون تغییر مجوز داده. fallback خودکار وجود ندارد.",
            Schema = new OpenApiSchema { Type = "string", Enum = new List<IOpenApiAny> { new OpenApiString("Organization"), new OpenApiString("Individual"), new OpenApiString("FreeTrial") } } });
        operation.Responses.TryAdd("409", new OpenApiResponse { Description = "Conflict عملیات/سهمیه؛ AI_OPERATION_ALREADY_COMPLETED / IN_PROGRESS / OUTCOME_UNKNOWN / PREVIOUSLY_FAILED. پاسخ خصوصی AI cache نمی‌شود و Provider دوباره صدا زده نمی‌شود." });
        foreach (var response in operation.Responses.Values)
        {
            response.Headers ??= new Dictionary<string, OpenApiHeader>();
            response.Headers.TryAdd("X-AI-Operation-Id", new OpenApiHeader { Description = "شناسهٔ intent پذیرفته‌شده؛ کلید پیگیری است، نه credential یا مجوز.", Schema = new OpenApiSchema { Type = "string", Format = "uuid" } });
        }
        return Task.CompletedTask;
    }
}
