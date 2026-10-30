using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class FileAndMessagingOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? string.Empty;
        if (route.StartsWith("api/v1/academic/exam-attempts/", StringComparison.Ordinal) && route.Contains("/attachments", StringComparison.Ordinal))
        {
            operation.Summary = context.Description.HttpMethod == "POST" ? "اتصال فایل آماده به نسخهٔ پاسخ آزمون" : "حذف پیوست با ایجاد نسخهٔ جدید پاسخ";
            operation.Description = "ابتدا POST /api/v1/files و وضعیت Ready لازم است. سیاست همان QuestionVersion تثبیت‌شده، مالکیت فایل، سازمان، Enrollment جاری، مهلت سرور، write lease و ExpectedRevisionNumber بررسی می‌شوند. ClientOperationId برای retry همان درخواست ثابت است؛ payload متفاوت با همان شناسه 409 می‌گیرد. پاسخ ExamAnswerReceiptDto و AnswerSetVersion جدید است. حذف پیوست فایل یا تاریخچه را پاک نمی‌کند. ذخیرهٔ متن با PUT answers فایل‌های جاری را حفظ می‌کند. پس از Finalize تغییر جدید مجاز نیست. UserId یا FileAssetId به‌تنهایی مجوز نیست.";
            foreach (var status in new[] { "400", "401", "403", "404", "409" })
                operation.Responses.TryAdd(status, new OpenApiResponse
                {
                    Description = "ProblemDetails: سیاست/سقف پیوست 400؛ احراز هویت 401؛ نقش نامجاز 403؛ فایل/تلاش نامجاز 404؛ نسخه، lease، مهلت، فایل غیرآماده یا Finalize برابر 409.",
                    Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = new OpenApiSchema { Type = "object" } } }
                });
        }
        if (context.Description.HttpMethod == "GET" && route == "api/v1/files/{fileId}/content")
        {
            operation.Summary = "دانلود بایت فایل با مجوز جاری";
            operation.Description = "پاسخ موفق بایت خام فایل است، نه JSON یا FileStreamResult سریال‌شده. Content-Type از فایل معتبر و Content-Disposition از نام دانلود تعیین می‌شود. Ready به‌تنهایی مجوز دانلود نیست؛ فایل unattached طبق scope و policy ممکن است 404 بدهد. هر درخواست، نشست و دسترسی جاری را بررسی می‌کند. Range روی stream قابل seek پشتیبانی می‌شود؛ Cache-Control=private, no-store و Vary=Authorization است.";
            operation.Responses["200"] = Binary("فایل کامل؛ مصرف‌کننده باید bytes یا Blob بخواند.");
            operation.Responses["206"] = Binary("بخشی از بایت‌ها برای Range معتبر روی stream قابل seek؛ Content-Range مشخص است.");
            operation.Responses["206"].Headers["Content-Range"] = Header("بازهٔ واقعی بایت‌ها و اندازهٔ کل");
            operation.Responses["416"] = new OpenApiResponse { Description = "Range خارج محدوده است؛ پاسخ موفق فایل نیست." };
            foreach (var status in new[] { "401", "403", "404", "409", "503" })
                operation.Responses.TryAdd(status, new OpenApiResponse
                {
                    Description = "ProblemDetails؛ خطای JSON نباید به‌عنوان فایل موفق ذخیره شود.",
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new() { Schema = new OpenApiSchema { Type = "object" } }
                    }
                });
        }
        if (context.Description.HttpMethod == "GET" && route == "api/v1/conversations/{conversationId}")
        {
            operation.Summary = "جزئیات مدیریتی Group یا Channel مجاز";
            operation.Description = "این مسیر ManagedConversationResult برمی‌گرداند و existence check عمومی گفتگو نیست. Direct و Saved از این مسیر مدیریتی 404 می‌گیرند؛ این پاسخ به معنی نبود پیام یا تاریخچه نیست. برای deep-link مجاز از /conversations/{conversationId}/summary و برای پیام‌ها از /conversations/{conversationId}/messages استفاده شود.";
            operation.Tags = [new OpenApiTag { Name = "Conversation Management" }];
        }
        if (context.Description.HttpMethod == "GET" && route == "api/v1/conversations/{conversationId}/summary")
        {
            operation.Summary = "Summary مجاز گفتگو مستقل از صفحه و فیلتر Inbox";
            operation.Description = "Direct، Saved، Group و Channel مجاز را با DTO موجود Inbox می‌خواند؛ archive مانع deep-link نیست. نشست، workspace جاری و participant فعال همچنان الزامی‌اند. شناسهٔ نامجاز یا context متفاوت 404 می‌گیرد. این مسیر مجوز مدیریت گروه یا ارسال پیام جدید ایجاد نمی‌کند.";
        }
        if (context.Description.HttpMethod == "POST" && route == "api/v1/conversations/{conversationId}/messages")
        {
            operation.Responses["422"] = new OpenApiResponse
            {
                Description = "MESSAGE_SAFETY_REVIEW_REQUIRED: وضعیت PendingReview، یا MESSAGE_SAFETY_BLOCKED: وضعیت Rejected. تغییر اعمال نشده و مجوز تحویل/دانلود تازه ایجاد نشده است. Ready فایل تأیید ایمنی نیست. پیش‌نویس حفظ شود و retry خودکار نشود؛ همان شناسه و payload همان نتیجه را می‌دهد. PendingReview وعدهٔ صف انسانی، SLA یا تأیید async نیست. دلیل داخلی و اطلاعات provider عمومی نیستند.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = SafetyFailure() }
                }
            };
        }
        return Task.CompletedTask;
    }

    private static OpenApiSchema SafetyFailure() => new()
    {
        Type = "object",
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["status"] = new() { Type = "integer", Enum = [new Microsoft.OpenApi.Any.OpenApiInteger(422)] },
            ["code"] = new() { Type = "string" },
            ["safetyState"] = new() { Type = "string", Enum = [new Microsoft.OpenApi.Any.OpenApiString("PendingReview"), new Microsoft.OpenApi.Any.OpenApiString("Rejected")] },
            ["operationState"] = new() { Type = "string", Enum = [new Microsoft.OpenApi.Any.OpenApiString("NotApplied")] },
            ["safetyReference"] = new() { Type = "string", Format = "uuid", Description = "شناسهٔ opaque رخداد ثبت‌شده؛ مجوز خواندن پرونده یا فایل نیست. در retry همان رخداد پایدار می‌ماند." },
            ["newMessageCreated"] = DeniedEffect(),
            ["newAttachmentsBound"] = DeniedEffect(),
            ["recipientDeliveryGranted"] = DeniedEffect(),
            ["recipientDownloadGranted"] = DeniedEffect(),
            ["retryable"] = DeniedEffect(),
            ["requiredAction"] = new() { Type = "string", Enum = [new Microsoft.OpenApi.Any.OpenApiString("KEEP_DRAFT_NO_AUTO_RETRY")] },
            ["correlationId"] = new() { Type = "string" },
            ["traceId"] = new() { Type = "string" }
        }
    };

    private static OpenApiSchema DeniedEffect() => new()
    {
        Type = "boolean", Enum = [new Microsoft.OpenApi.Any.OpenApiBoolean(false)]
    };

    private static OpenApiResponse Binary(string description) => new()
    {
        Description = description,
        Content = new Dictionary<string, OpenApiMediaType>
        {
            ["application/octet-stream"] = new() { Schema = new OpenApiSchema { Type = "string", Format = "binary" } }
        },
        Headers = new Dictionary<string, OpenApiHeader>
        {
            ["Content-Type"] = Header("MIME واقعی فایل طبق metadata معتبر؛ ممکن است image/png، image/jpeg، application/pdf یا text/plain باشد."),
            ["Content-Disposition"] = Header("attachment؛ نام دانلود با encoding مناسب"),
            ["Content-Length"] = new() { Description = "اندازهٔ بدنه بر حسب بایت", Schema = new OpenApiSchema { Type = "integer", Format = "int64" } }
        }
    };
    private static OpenApiHeader Header(string description) => new() { Description = description, Schema = new OpenApiSchema { Type = "string" } };
}
