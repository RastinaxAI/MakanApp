using MakanApp.Application.Ai;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class AdaptiveAiSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if (context.JsonTypeInfo.Type == typeof(AiAssistantProfile))
        {
            if (schema.Properties.TryGetValue("gradeLevel", out var grade)) { grade.Type = "integer"; grade.Minimum = 1; grade.Maximum = 12; grade.Nullable = true; }
            if (schema.Properties.TryGetValue("proficiency", out var proficiency))
            { proficiency.Type = "string"; proficiency.Nullable = true; proficiency.Enum = Enum.GetNames<AiAssistantProficiency>().Select(x => (IOpenApiAny)new OpenApiString(x)).ToList(); }
            if (schema.Properties.TryGetValue("goals", out var goals))
            { goals.MaxItems = 8; goals.Nullable = true; goals.Items = new() { Type = "string", MinLength = 1, MaxLength = 80 }; }
            schema.Description = "اختیاری و مستقل از Authorization؛ grade عدد صحیح سخت‌گیرانه، proficiency با حروف دقیق، goals حداکثر ۸ متن غیرخالی با سقف ۸۰ Unicode code point. null goals به آرایهٔ خالی wire تبدیل می‌شود.";
            schema.AdditionalPropertiesAllowed = false;
        }
        if (context.JsonTypeInfo.Type == typeof(SendAiChatCommand))
        {
            if (schema.Properties.TryGetValue("message", out var message)) { message.MinLength = 1; message.MaxLength = 4000; }
            if (schema.Properties.TryGetValue("selectedSources", out var sources)) sources.MaxItems = 32;
            if (schema.Properties.TryGetValue("selectedSourceIds", out var ids)) ids.MaxItems = 32;
            schema.Description = "History را Backend می‌خواند؛ ورودی history authority نیست. پیش‌فرض بدون Source برابر General و با Source برابر Auto/SourceFocused است. SourceIntent.General فقط در Auto اجازهٔ fallback با provenance می‌دهد. SourcesOnly بدون fallback است.";
        }
        return Task.CompletedTask;
    }
}
