using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class PersonalClassInputSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        schema.Properties ??= new Dictionary<string, OpenApiSchema>();
        schema.Required ??= new HashSet<string>();
        var personal = type.Namespace == "MakanApp.Application.Academic" && type.Name.Contains("PersonalClass", StringComparison.Ordinal);
        var assessment = type.Namespace == "MakanApp.Application.Assessment";
        if ((!personal && !assessment) || !(type.Name.EndsWith("Command", StringComparison.Ordinal) || type.Name.EndsWith("Input", StringComparison.Ordinal) || type.Name.EndsWith("Query", StringComparison.Ordinal))) return Task.CompletedTask;
        foreach (var (name, field) in schema.Properties)
        {
            switch (name)
            {
                case "title": Text(field, 1, 200); break;
                case "purpose": Text(field, 1, 2000); break;
                case "description": Text(field, type.Name.Contains("Assignment", StringComparison.Ordinal) ? 1 : 0, personal ? 4000 : 10000); break;
                case "reason": Text(field, 1, 500); break;
                case "correctionReason": Text(field, 1, EvaluationRevision.MaximumCorrectionReasonLength); break;
                case "prompt": Text(field, 1, QuestionVersion.MaximumPromptLength); break;
                case "text" when type == typeof(ExamQuestionOptionCommand): Text(field, 1, QuestionOption.MaximumTextLength); break;
                case "answerText": Text(field, 0, SubmissionAttempt.MaximumAnswerLength); break;
                case "textAnswer": field.MaxLength = AnswerRevision.MaximumTextAnswerLength; break;
                case "learnerFeedback": case "guardianVisibleFeedback": case "teacherPrivateNote": case "evaluatorPrivateNote": Text(field, 0, 10000); break;
                case "capacity": case "suggestedCapacity": case "durationMinutes": case "maxAttempts": case "maximumCount": case "maximumTotalBytes": field.Minimum = 1; break;
                case "order": field.Minimum = 1; break;
                case "maxScore": case "score": case "awardedScore":
                    field.Minimum = name == "maxScore" || name == "score" && type.Name.Contains("Question", StringComparison.Ordinal) ? .01m : 0;
                    field.Maximum = AssignmentVersion.MaximumScoreValue; field.MultipleOf = .01m;
                    field.Description = "حداکثر دو رقم اعشار؛ سقف واقعی نمرهٔ ارزیابی از AssignmentVersion یا QuestionVersion تثبیت‌شده گرفته می‌شود، نه مقدار جهانی ۲۰."; break;
                case "attachmentFileIds": field.MaxItems = 20; field.UniqueItems = true; break;
                case "options": field.Description = "سؤال ObjectiveSingleChoice حداقل دو گزینه، دقیقاً یک پاسخ صحیح، ترتیب یکتا و متن‌های غیرتکراری لازم دارد؛ سؤال Descriptive گزینه ندارد. سقف تعداد حدسی تعریف نشده است."; break;
                case "allowedContentTypes": field.Description = "هنگام فعال‌بودن پیوست باید حداقل یک MIME معتبر و مجاز Storage تعیین شود؛ مجوز نهایی از policy سؤال است."; break;
                case "page": field.Minimum = 1; field.Maximum = personal ? 1_000_000 : 100_000; field.Default = new OpenApiInteger(1); break;
                case "pageSize": field.Minimum = 1; field.Maximum = 100; field.Default = new OpenApiInteger(personal ? 30 : 25); break;
                case "search": field.MaxLength = 200; break;
                case "dueAtUtc": field.Description = "UTC با پسوند Z و پس از زمان جاری سرور؛ deadline هنگام ساخت/ویرایش دوباره بررسی می‌شود."; field.Format = "date-time"; break;
                case "availableFromUtc": case "availableUntilUtc": field.Description = "UTC با پسوند Z؛ پایان بازه باید بعد از آغاز باشد. زمان دستگاه authority نیست."; field.Format = "date-time"; break;
            }
            if (name.Contains("Version", StringComparison.Ordinal) && field.Type == "string")
            { field.Format = "byte"; field.Description = "Base64 استاندارد rowversion هشت‌بایتی؛ نسخهٔ کهنه conflict می‌دهد."; }
            if (name == "clientOperationId") field.Description = "UUID غیرخالیِ قصد عملیات؛ در retry همان شناسه و همان payload را نگه دارید؛ payload متفاوت conflict دارد.";
        }
        Required(schema, type.Name switch
        {
            "PersonalClassRequestInput" => ["title", "purpose", "suggestedCapacity"],
            "CreatePersonalClassRequestCommand" => ["clientOperationId", "details"],
            "EditPersonalClassRequestCommand" => ["expectedVersion", "details"],
            "CreateAssignmentDraftCommand" => ["title", "description", "dueAtUtc", "maxAttempts", "maxScore"],
            "UpdateAssignmentDraftCommand" => ["title", "description", "dueAtUtc", "maxAttempts", "maxScore", "expectedAssignmentRowVersion", "expectedVersionRowVersion"],
            "CreateExamDraftCommand" => ["title", "availableFromUtc", "availableUntilUtc", "durationMinutes", "maxAttempts", "maxScore", "randomizationPolicy"],
            "UpdateExamDraftCommand" => ["title", "availableFromUtc", "availableUntilUtc", "durationMinutes", "maxAttempts", "maxScore", "randomizationPolicy", "expectedVersionRowVersion"],
            "AddExamQuestionCommand" => ["order", "type", "prompt", "score", "expectedVersionRowVersion"],
            "UpdateExamQuestionCommand" => ["order", "type", "prompt", "score", "expectedVersionRowVersion", "expectedQuestionRowVersion"],
            _ => []
        });
        return Task.CompletedTask;
    }
    private static void Text(OpenApiSchema field, int min, int max)
    {
        field.MinLength = min; field.MaxLength = null;
        // maxLength روی متن خام، فاصله‌های ابتدا/انتها را هم می‌شمارد؛ Domain طول Trimشده را محدود می‌کند.
        field.Pattern = "^\\s*" + (min == 0 ? "(?:" : "") + "(?:\\S|\\S[\\s\\S]{0," + (max - 2) + "}\\S)" + (min == 0 ? ")?" : "") + "\\s*$";
        field.Description = "محدودیت طول پس از Trim اعمال می‌شود؛ فیلد اجباری نباید فقط whitespace باشد.";
        field.Extensions["x-normalization"] = new OpenApiString("trim");
        field.Extensions["x-normalized-max-length"] = new OpenApiInteger(max);
    }
    private static void Required(OpenApiSchema schema, string[] names)
    { foreach (var name in names) if (schema.Properties.ContainsKey(name)) schema.Required.Add(name); }
}
