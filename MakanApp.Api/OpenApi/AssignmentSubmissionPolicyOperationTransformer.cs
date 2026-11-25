using MakanApp.Application.Assessment;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class AssignmentSubmissionPolicyOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? "";
        var method = context.Description.HttpMethod ?? "GET";
        if (route == "api/v1/academic/assignments/{assignmentId}/submission-policy" && method == "PATCH")
        {
            operation.Summary = "تمدید مهلت یا تغییر پذیرش پاسخ دیرهنگام تکلیف منتشرشده";
            operation.Description = "فقط dueAtUtc و allowLateSubmission تغییر می‌کنند. مهلت فقط به UTC آینده تمدید می‌شود؛ کوتاه‌کردن ممنوع است. " +
                "برای تغییر Late به‌تنهایی همان مهلت قبلی، حتی گذشته، فرستاده می‌شود. عنوان، محتوا، MaxScore، تلاش‌های موجود و رسیدهای تاریخی ثابت می‌مانند. " +
                "Manager سازمان یا Teacher دارای Assignment جاری همان کلاس مجاز است؛ در PersonalClass اختیار صریح و جاری AuthorAssignments لازم است. " +
                "هر دو rowversion از GET تکلیف لازم‌اند. نسخهٔ stale، شامل Retry پس از تغییر موفق، مطابق قرارداد موجود 412 CONCURRENCY_CONFLICT است؛ " +
                "ابتدا GET مجدد، سپس تصمیم آگاهانهٔ کاربر. درخواست یکسان با نسخه‌های جاری تغییر یا Audit اضافه نمی‌سازد. تغییر واقعی و Audit در یک تراکنش ثبت می‌شوند.";
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object", AdditionalPropertiesAllowed = false,
                            Required = new HashSet<string> { "dueAtUtc", "allowLateSubmission", "expectedAssignmentRowVersion", "expectedVersionRowVersion" },
                            Properties = new Dictionary<string, OpenApiSchema>
                            {
                                ["dueAtUtc"] = new() { Type = "string", Format = "date-time", Description = "UTC صریح؛ زمان آینده برای تمدید، یا عین مهلت فعلی برای تغییر Late." },
                                ["allowLateSubmission"] = new() { Type = "boolean", Description = "اجباری؛ حذف فیلد با false یکسان نیست." },
                                ["expectedAssignmentRowVersion"] = new() { Type = "string", Description = "نسخهٔ Base64 هشت‌بایتی aggregate از آخرین GET." },
                                ["expectedVersionRowVersion"] = new() { Type = "string", Description = "نسخهٔ Base64 هشت‌بایتی AssignmentVersion از همان GET." }
                            }
                        }
                    }
                }
            };
            operation.Responses.TryAdd("412", new OpenApiResponse());
            operation.Responses["412"].Description = "CONCURRENCY_CONFLICT؛ حداقل یکی از دو rowversion معتبر و جاری نیست. وضعیت تازه را بخوانید؛ Retry کور انجام نشود.";
        }

        var deadlineOperation = method == "POST" && route == "api/v1/academic/assignments/{assignmentId}/attempts" ||
            route.StartsWith("api/v1/academic/submission-attempts/", StringComparison.Ordinal) &&
            (method == "PATCH" && route.EndsWith("/draft", StringComparison.Ordinal) ||
             method == "POST" && (route.EndsWith("/attachments", StringComparison.Ordinal) || route.EndsWith("/submit", StringComparison.Ordinal)) ||
             method == "DELETE" && route.Contains("/attachments/", StringComparison.Ordinal));
        if (!deadlineOperation) return Task.CompletedTask;

        operation.Description = (operation.Description ?? "") + "\nمهلت با زمان پذیرش UTC سرور بررسی می‌شود؛ برابری با مهلت مجاز است. بعد از مهلت، " +
            "allowLateSubmission=true مانع زمانی را برمی‌دارد و سایر مجوزها برقرارند. SUBMISSION_DEADLINE_PASSED=409 یعنی همین تغییر اعمال نشده؛ " +
            "موفقیت Upload قبلی را نقض نمی‌کند. متن «مهلت تحویل این تکلیف به پایان رسیده است.» نمایش داده شود، تکلیف و تلاش دوباره خوانده شوند و فایل معتبر حفظ شود. " +
            "ارسال مجدد نهاییِ تلاش قبلاً Submitشده، رسید قبلی را بدون بازنویسی IsLate برمی‌گرداند.";
        operation.Responses.TryAdd("409", new OpenApiResponse { Description = "ProblemDetails برای تعارض عملیات." });
        var response = operation.Responses["409"];
        response.Content["application/problem+json"] = new OpenApiMediaType
        {
            Schema = new OpenApiSchema
            {
                AllOf = [ApiContractDocumentTransformer.ProblemReference()],
                Description = "فیلدهای زیر فقط برای code=SUBMISSION_DEADLINE_PASSED و پس از احراز مجوز منبع برمی‌گردند؛ شامل متن پاسخ یا دادهٔ خصوصی دانش‌آموز نیستند.",
                Properties = new Dictionary<string, OpenApiSchema>
                {
                    ["assignmentId"] = Identifier(), ["assignmentVersionId"] = Identifier(), ["classId"] = Identifier(),
                    ["classKind"] = new() { Type = "string" }, ["attemptId"] = Identifier(nullable: true),
                    ["requestTimeUtc"] = new() { Type = "string", Format = "date-time", Description = "زمان بررسی و پذیرش عملیات پس از قفل‌های لازم، نه ساعت مرورگر یا زمان تاریخی رخداد دیگر." },
                    ["dueAtUtc"] = new() { Type = "string", Format = "date-time" },
                    ["allowLateSubmission"] = new() { Type = "boolean" },
                    ["attemptStatus"] = new() { Type = "string", Nullable = true },
                    ["assignmentStatus"] = new() { Type = "string" },
                    ["assignmentVersionIsPublished"] = new() { Type = "boolean" },
                    ["requiredAction"] = new() { Type = "string", Example = new OpenApiString("REFETCH_ASSIGNMENT_AND_ATTEMPT") },
                    ["operationState"] = new() { Type = "string", Example = new OpenApiString("NotApplied") },
                    ["retryable"] = new() { Type = "boolean", Example = new OpenApiBoolean(false) }
                }
            }
        };
        response.Extensions["x-deadline-error-code"] = new OpenApiString(AssessmentErrorCodes.SubmissionDeadlinePassed);
        return Task.CompletedTask;
    }

    private static OpenApiSchema Identifier(bool nullable = false) => new() { Type = "string", Format = "uuid", Nullable = nullable };
}
