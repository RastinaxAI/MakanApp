using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class PersonalAcademicOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? "";
        if (route.Contains("academic-permissions", StringComparison.Ordinal))
            operation.Description = "مجوز آموزشی مستقل از نقش محلی است. Platform با permission مستقل به دورهٔ عضویت مدیر اختیار می‌دهد؛ مدیر فقط زیرمجموعهٔ اختیار مؤثر خود را به Teacher تفویض می‌کند. ClientOperationId تکرارپذیر است؛ ExpectedMembershipVersion و ExpectedGrantVersion برای کنترل هم‌زمانی لازم‌اند. لغو منبع، تفویض وابسته را بی‌اثر می‌کند. این مسیر دسترسی به پاسخ یا نمرهٔ خصوصی نمی‌دهد.";
        else if (route.StartsWith("api/v1/academic/personal-classes/", StringComparison.Ordinal))
            operation.Description = "موتور آموزشی مشترک با ClassRef.Kind=PersonalClass و عضویت محلی جاری؛ بدون Organization یا Enrollment ساختگی. فهرست با page/pageSize، search و published روی سرور صفحه‌بندی می‌شود. پیش‌فرض ۲۵ و سقف ۱۰۰ است. هنرجو فقط منابع منتشرشدهٔ مجاز را می‌بیند؛ ایجاد نیازمند اختیار صریح AuthorAssignments یا AuthorExams است.";
        else if (route.StartsWith("api/v1/academic/", StringComparison.Ordinal) &&
            (route.Contains("assignment", StringComparison.Ordinal) || route.Contains("submission", StringComparison.Ordinal) || route.Contains("exam", StringComparison.Ordinal) || route.Contains("evaluation", StringComparison.Ordinal)))
            operation.Description = (operation.Description ?? "") + "\nمنبع با شناسهٔ واقعی به OrganizationClass یا PersonalClass resolve می‌شود؛ شناسهٔ کلاینت مجوز نیست. در PersonalClass، عضویت جاری و اختیار آموزشی صریح لازم است؛ انتقال مدیریت یا عضویت دوباره، اختیار یا تلاشِ دورهٔ قبلی را زنده نمی‌کند. وضعیت Archived نوشتن جدید را می‌بندد. نمرهٔ Draft و یادداشت خصوصی در پاسخ هنرجو نمایش داده نمی‌شوند. رسید Finalize با FinalizationMode=Deadline نشان‌دهندهٔ تثبیت پاسخ‌های پذیرفته‌شده در سررسید سرور است.";
        else return Task.CompletedTask;

        foreach (var status in new[] { "400", "401", "403", "404", "409", "412" })
            operation.Responses.TryAdd(status, new OpenApiResponse
            {
                Description = "خطای پایدار ProblemDetails؛ code و correlationId برای تصمیم کلاینت و پیگیری.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = new OpenApiSchema { Type = "object" } }
                }
            });
        return Task.CompletedTask;
    }
}
