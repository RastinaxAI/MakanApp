using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class ApiContractDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
        document.Components.Schemas ??= new Dictionary<string, OpenApiSchema>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "Opaque",
            Description = "accessToken همان credential تصادفی و opaque نشست است؛ JWT نیست و نباید decode شود. Refresh با refreshToken بدنه انجام می‌شود. مجوز Platform، عضویت و capability مستقل از Bearer بررسی می‌شوند."
        };
        document.Components.Schemas["MakanProblemDetails"] = new OpenApiSchema
        {
            Type = "object", AdditionalPropertiesAllowed = true,
            Required = new HashSet<string> { "status", "code", "traceId", "correlationId" },
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["type"] = new() { Type = "string" }, ["title"] = new() { Type = "string" },
                ["status"] = new() { Type = "integer", Minimum = 400, Maximum = 599 },
                ["detail"] = new() { Type = "string", Nullable = true }, ["instance"] = new() { Type = "string", Nullable = true },
                ["code"] = new() { Type = "string", Description = "کد پایدار ماشین‌خوان؛ متن detail را برای تصمیم برنامه parse نکنید." },
                ["traceId"] = new() { Type = "string" }, ["correlationId"] = new() { Type = "string" },
                ["errors"] = new() { Type = "object", Nullable = true, AdditionalProperties = new OpenApiSchema
                    { Type = "array", Items = new OpenApiSchema { Type = "string" } } }
            }
        };
        return Task.CompletedTask;
    }

    internal static OpenApiSchema ProblemReference() => new()
    { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = "MakanProblemDetails" } };
}
