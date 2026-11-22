using MakanApp.Application.Commerce;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class CommercialAccessSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if ((context.JsonTypeInfo.Type == typeof(CommercialSummaryDto) || context.JsonTypeInfo.Type == typeof(ManagerCommerceSummaryDto))
            && schema.Properties.TryGetValue("enforcementEnabled", out var enforcement))
            enforcement.Description = "false یعنی Subscription/Entitlement/Quota تجاری Gate قابلیت‌های عادی نیستند؛ State و سوابق مالی واقعی باقی‌اند. این مقدار مجوز منبع، عضویت، Privacy یا Safety ایجاد نمی‌کند.";
        return Task.CompletedTask;
    }
}
