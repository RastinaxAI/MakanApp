using MakanApp.Application.Knowledge;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class KnowledgeContractTransformer : IOpenApiSchemaTransformer, IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        var type = context.JsonTypeInfo.Type;
        if (type == typeof(KnowledgeProcessingStatus)) SetEnum("state", Enum.GetNames<KnowledgeProcessingState>());
        if (type == typeof(KnowledgeRetrieveResponse)) SetEnum("status", ["Ok", "Empty"]);
        if (type == typeof(KnowledgeRagResponse)) SetEnum("status", ["Ok", "InsufficientEvidence"]);
        if (type == typeof(KnowledgeQueryCommand))
        {
            if (schema.Properties.TryGetValue("query", out var query)) { query.MinLength = 1; query.MaxLength = 4000; }
            if (schema.Properties.TryGetValue("sources", out var sources)) { sources.MinItems = 1; sources.MaxItems = 32; }
            if (schema.Properties.TryGetValue("maxResults", out var maximum)) { maximum.Minimum = 1; maximum.Maximum = 20; }
        }
        if (type == typeof(KnowledgeSourceVersion) && schema.Properties.TryGetValue("version", out var version)) version.Minimum = 1;
        return Task.CompletedTask;
        void SetEnum(string name, string[] values)
        {
            if (schema.Properties.TryGetValue(name, out var property))
                property.Enum = values.Select(value => (IOpenApiAny)new OpenApiString(value)).ToList();
        }
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? string.Empty;
        if (!route.StartsWith("api/v1/ai/knowledge/", StringComparison.Ordinal) &&
            !(route.StartsWith("api/v1/sources/", StringComparison.Ordinal) && route.Contains("/knowledge/", StringComparison.Ordinal))) return Task.CompletedTask;
        operation.Description = "نشست فعال، Context و SourceVersion دقیق در Backend بازبینی می‌شوند؛ Library بسته است. " +
            "Feature Gate باید فعال باشد. ClientOperationId در body کلید intent است؛ Headerهای Operation/Payer مسیر Business AI برای این API استفاده نمی‌شوند. " +
            "RAG از سهمیهٔ SimpleText موجود استفاده می‌کند؛ replay تکمیل‌شده409 است و پاسخ خصوصی ذخیره نمی‌شود. " +
            "Citation مجوز نیست؛ پس از پاسخ نیز دسترسی دوباره بررسی می‌شود. Ingest و lifecycle به writer و ExpectedVersion جاری نیاز دارند.";
        operation.Responses ??= new();
        foreach (var (status, description) in new[]
        {
            ("401", "نشست نامعتبر."), ("403", "مجوز مدیریت منبع یا Entitlement تجاری وجود ندارد."),
            ("404", "منبع ناموجود یا خارج از دسترسی جاری؛ concealment."), ("409", "نسخه/intent/وضعیت پردازش یا سهمیه ناسازگار است."),
            ("422", "قرارداد Knowledge یا Query معتبر نیست."), ("429", "محدودیت درخواست حساب."),
            ("502", "پاسخ یا Citation معتبر و grounded نیست."), ("503", "Feature یا dependency آماده نیست."),
            ("504", "مهلت HTTP یا polling تمام شده است؛ retry خودکار وجود ندارد.")
        }) operation.Responses.TryAdd(status, new OpenApiResponse { Description = description, Content = new Dictionary<string, OpenApiMediaType>
        { ["application/problem+json"] = new() { Schema = new() { Reference = new() { Type = ReferenceType.Schema, Id = "ProblemDetails" } } } } });
        return Task.CompletedTask;
    }
}
