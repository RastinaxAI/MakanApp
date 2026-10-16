using System.Text.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class StringEnumSchemaTransformer(IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> mvcOptions)
    : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var enumType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (enumType.IsEnum && Enum.GetValues(enumType).Cast<object>().FirstOrDefault() is { } sample &&
            JsonSerializer.SerializeToElement(sample, enumType, mvcOptions.Value.JsonSerializerOptions).ValueKind == JsonValueKind.String)
        {
            // در نسخهٔ جاری .NET 9، literalهای رشته‌ای ساخته می‌شوند ولی type ممکن است حذف شود.
            // enum/nullable و نمایش Flags تولیدشده حفظ می‌شوند؛ رفتار JSON برنامه تغییر نمی‌کند.
            schema.Type = "string";
            schema.Format = null;
        }
        return Task.CompletedTask;
    }
}
