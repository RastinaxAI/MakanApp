using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Text.Json;

namespace MakanApp.Api.OpenApi;

public sealed class SchoolOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? "";
        var method = context.Description.HttpMethod;
        var school = route == "api/v1/organization/members" || route.Contains("guardian-invitations", StringComparison.Ordinal) ||
            route.StartsWith("api/v1/academic/", StringComparison.Ordinal) &&
            (route.Contains("lessons", StringComparison.Ordinal) || method == "GET" && (route.EndsWith("/learners", StringComparison.Ordinal) || route.EndsWith("/teachers", StringComparison.Ordinal) || route.EndsWith("/courses", StringComparison.Ordinal) || route.EndsWith("/periods", StringComparison.Ordinal)));
        if (route == "api/v1/academic/schedule")
        {
            operation.Description = "بازهٔ fromUtc و toUtc اجباری است؛ هر دو UTC با پسوند Z، پایان پس از آغاز و حداکثر یک سال پس از آغاز. حذف یا بازهٔ نامعتبر: 400 SESSION_TIME_INVALID. مجوزهای فعلی تقویم حفظ می‌شوند.";
            foreach (var parameter in operation.Parameters.Where(x => x.Name is "fromUtc" or "toUtc"))
            { parameter.Required = true; parameter.Description = "زمان UTC اجباری با پسوند Z"; parameter.Example = new OpenApiString(parameter.Name == "fromUtc" ? "2026-11-01T00:00:00Z" : "2026-11-08T00:00:00Z"); }
            Error(operation, "400", "SESSION_TIME_INVALID");
        }
        if (route == "api/v1/invitations/{invitationId}/accept")
        {
            operation.Description = "پذیرش فقط توسط حساب مقصد در نشست معتبر. دعوت Person Link نیازمند confirmPersonLink و دعوت والد نیازمند confirmGuardianRelation=true است. پذیرش والد، Membership/Role و GuardianRelation را اتمیک ثبت می‌کند؛ بازپخش رسید رابطهٔ پایان‌یافته را فعال نمی‌کند.";
            Request(operation, """{"confirmPersonLink":false,"confirmGuardianRelation":true}""");
            Error(operation, "400", "GUARDIAN_INVITATION_CONSENT_REQUIRED");
            if (operation.Responses.TryGetValue("200", out var accepted))
                foreach (var media in accepted.Content.Values) media.Example = Example("""{"invitationId":"00000000-0000-0000-0000-000000000001","organizationId":"00000000-0000-0000-0000-000000000002","organizationName":"آموزشگاه","membershipId":"00000000-0000-0000-0000-000000000003","roleAssignmentId":"00000000-0000-0000-0000-000000000004","role":"Parent","alreadyAccepted":false,"organizationPersonId":null,"guardianRelationId":"00000000-0000-0000-0000-000000000005"}""");
        }
        if (route == "api/v1/invitations" && method == "GET")
        {
            operation.Description = "inbox دعوت‌های حساب احرازشده؛ دعوت والد نیازمند requiresGuardianConsent و تأیید صریح است. organizationPersonId متعلق به Person Link است و با guardianLearnerOrganizationPersonId یکی نیست.";
            if (operation.Responses.TryGetValue("200", out var inbox))
                foreach (var media in inbox.Content.Values) media.Example = Example("""[{"id":"00000000-0000-0000-0000-000000000001","organizationId":"00000000-0000-0000-0000-000000000002","organizationName":"آموزشگاه","invitedByUserId":"00000000-0000-0000-0000-000000000003","role":"Parent","status":"Pending","expiresAtUtc":"2026-11-01T12:00:00Z","organizationPersonId":null,"organizationPersonDisplayName":null,"guardianLearnerOrganizationPersonId":"00000000-0000-0000-0000-000000000004","guardianLearnerDisplayName":"فراگیر","requiresPersonLinkConsent":false,"requiresGuardianConsent":true,"nextAction":"CONFIRM_GUARDIAN_RELATION"}]""");
        }
        if (!school) return Task.CompletedTask;
        operation.Description = "نشست معتبر و Workspace سازمانی جاری لازم است؛ OrganizationId ارسالی از کلاینت مجوز نیست. " +
            (route.Contains("lessons", StringComparison.Ordinal)
                ? "Manager یا Teacher با Assignment فعال همان کلاس مجاز به نوشتن و دیدن Draft است. Student با Enrollment فعال فقط Published را می‌بیند. Parent مجاز نیست (OPEN_PRODUCT_DECISION). محتوا plain text و انتشار در این مرحله immutable است. version/expectedVersion برابر Base64 rowversion هشت‌بایتی است. Create با clientOperationId بازپخش‌پذیر؛ تغییر payload همان عملیات 409؛ نسخهٔ کهنه 412. پیوست جدید باید FileAsset آمادهٔ همان سازمان و متعلق به فرستنده باشد (حداکثر ۱۰). DownloadUrl نسبی است و در هر درخواست مجوز جاری بررسی می‌شود."
                : route.EndsWith("/learners", StringComparison.Ordinal) || route.EndsWith("/teachers", StringComparison.Ordinal)
                    ? "فقط Manager یا Teacher با Assignment فعال همان کلاس. شناسهٔ رابطه از User/Person/Membership جداست؛ وضعیت پیش‌فرض Active. مرجع یا مجوز نامعتبر 404."
                    : route.Contains("guardian-invitations", StringComparison.Ordinal)
                        ? "فقط Manager جاری. یکی از username دقیق یا phoneNumber قابل نرمال‌سازی به حساب موجود و تلفن تأییدشده؛ هیچ fuzzy matching یا ایجاد حساب انجام نمی‌شود. انقضای UTC در آینده و حداکثر ۳۰ روز. خطای مقصد 404 بدون افشای هویت. دعوت در inbox مقصد و nextAction=CONFIRM_GUARDIAN_RELATION نمایش داده می‌شود؛ پذیرش صریح در endpoint موجود لازم است. Messaging privacy تغییر نمی‌کند."
                        : "فقط Manager سازمان جاری. فهرست اعضا شامل حساب/شخص/عضویت/نقش‌های مستقل است؛ profilePhoto فقط طبق سیاست فعلی Avatar و در غیر این صورت null. کاتالوگ‌ها شامل موارد استفاده‌نشده هم هستند.");
        foreach (var p in operation.Parameters)
        {
            if (p.Name.Equals("page", StringComparison.OrdinalIgnoreCase)) { p.Description = "صفحه از ۱؛ حداکثر 100000"; p.Example = new OpenApiInteger(1); }
            if (p.Name.Equals("pageSize", StringComparison.OrdinalIgnoreCase)) { p.Description = "۱ تا ۱۰۰؛ پیش‌فرض ۵۰"; p.Example = new OpenApiInteger(50); }
            if (p.Name.Equals("search", StringComparison.OrdinalIgnoreCase)) p.Description = "جست‌وجوی trimشده تا ۱۰۰ نویسه؛ فقط در محدودهٔ مجاز";
        }
        operation.Description += " فهرست‌ها items/totalCount/page/pageSize/dataAsOfUtc دارند؛ ترتیب پایدار با شناسهٔ یکتا به‌عنوان tie-breaker. status اختیاری و enum رشته‌ای؛ فیلدهای اختیاری null هستند. تغییر هم‌زمان داده بین صفحات snapshot واحد تضمین نمی‌کند.";
        Error(operation, "400", "SCHOOL_REQUEST_INVALID"); Error(operation, "401", "AUTH_REQUIRED");
        Error(operation, "403", "WORKSPACE_NOT_ALLOWED");
        if (route.Contains("lessons", StringComparison.Ordinal)) { Error(operation, "404", "LESSON_NOT_FOUND"); Error(operation, "409", "LESSON_STATE_CONFLICT"); Error(operation, "412", "LESSON_VERSION_CONFLICT"); }
        else if (route.Contains("guardian-invitations", StringComparison.Ordinal)) { Error(operation, "404", "GUARDIAN_INVITATION_TARGET_NOT_AVAILABLE"); Error(operation, "409", "GUARDIAN_INVITATION_CONFLICT"); Error(operation, "412", "CONCURRENCY_CONFLICT"); Error(operation, "429", "RATE_LIMITED"); }
        else if (route.EndsWith("/learners", StringComparison.Ordinal) || route.EndsWith("/teachers", StringComparison.Ordinal)) Error(operation, "404", "CLASS_NOT_FOUND");

        if (route.Contains("guardian-invitations", StringComparison.Ordinal))
        {
            Request(operation, route.EndsWith("/revoke", StringComparison.Ordinal)
                ? """{"expectedRowVersion":"AAAAAAAAAAE=","reason":"لغو دعوت والد"}"""
                : """{"clientOperationId":"00000000-0000-0000-0000-000000000001","username":"test.parent","phoneNumber":null,"expiresAtUtc":"2026-11-01T12:00:00Z","reason":"تأیید رابطه والد"}""");
        }
        else if (route.Contains("lessons", StringComparison.Ordinal) && method != "GET")
            Request(operation, route.EndsWith("/publish", StringComparison.Ordinal) ? """{"expectedVersion":"AAAAAAAAAAE="}"""
                : method == "PATCH" ? """{"title":"درس اول","body":"متن جایگزین کامل","displayOrder":0,"fileAssetIds":[],"expectedVersion":"AAAAAAAAAAE="}"""
                : """{"clientOperationId":"00000000-0000-0000-0000-000000000001","title":"درس اول","body":"متن درس","displayOrder":0,"fileAssetIds":[]}""");

        foreach (var response in operation.Responses.Where(x => x.Key is "200" or "201"))
            foreach (var content in response.Value.Content.Where(x => x.Key.Contains("json", StringComparison.Ordinal)))
                content.Value.Example = Example(route.Contains("lessons", StringComparison.Ordinal) && !route.EndsWith("/lessons", StringComparison.Ordinal) || method == "POST" && route.Contains("lessons", StringComparison.Ordinal)
                    ? LessonExample
                    : route.Contains("guardian-invitations", StringComparison.Ordinal) ? GuardianExample : PageExample(route));
        return Task.CompletedTask;
    }

    private static void Request(OpenApiOperation operation, string json)
    {
        if (operation.RequestBody is not null)
            foreach (var content in operation.RequestBody.Content.Where(x => x.Key.Contains("json", StringComparison.Ordinal))) content.Value.Example = Example(json);
    }
    private static IOpenApiAny Example(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ConvertElement(document.RootElement);
        static IOpenApiAny ConvertElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var result = new OpenApiObject();
                foreach (var property in element.EnumerateObject()) result[property.Name] = ConvertElement(property.Value);
                return result;
            }
            if (element.ValueKind == JsonValueKind.Array)
            {
                var result = new OpenApiArray();
                foreach (var item in element.EnumerateArray()) result.Add(ConvertElement(item));
                return result;
            }
            return element.ValueKind switch
            {
                JsonValueKind.String => new OpenApiString(element.GetString()),
                JsonValueKind.Number => new OpenApiInteger(element.GetInt32()),
                JsonValueKind.True => new OpenApiBoolean(true), JsonValueKind.False => new OpenApiBoolean(false),
                _ => new OpenApiNull()
            };
        }
    }
    private static void Error(OpenApiOperation operation, string status, string code)
    {
        operation.Responses.TryAdd(status, new OpenApiResponse { Description = "ProblemDetails؛ مجوز، وضعیت یا نسخهٔ معتبر لازم است." });
        operation.Responses[status].Content.TryAdd("application/problem+json", new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = "object" },
            Example = new OpenApiObject { ["status"] = new OpenApiInteger(int.Parse(status, System.Globalization.CultureInfo.InvariantCulture)), ["code"] = new OpenApiString(code), ["correlationId"] = new OpenApiString("example-correlation") }
        });
    }
    private const string LessonExample = """{"id":"00000000-0000-0000-0000-000000000001","organizationId":"00000000-0000-0000-0000-000000000002","classId":"00000000-0000-0000-0000-000000000003","courseId":"00000000-0000-0000-0000-000000000004","title":"درس اول","body":"متن درس","displayOrder":0,"status":"Draft","revisionNumber":1,"version":"AAAAAAAAAAE=","createdByUserId":"00000000-0000-0000-0000-000000000005","createdAtUtc":"2026-10-28T12:00:00Z","updatedAtUtc":null,"publishedAtUtc":null,"attachments":[],"replayed":false}""";
    private const string GuardianExample = """{"id":"00000000-0000-0000-0000-000000000001","organizationId":"00000000-0000-0000-0000-000000000002","learnerOrganizationPersonId":"00000000-0000-0000-0000-000000000003","status":"Pending","expiresAtUtc":"2026-11-01T12:00:00Z","version":"AAAAAAAAAAE=","replayed":false,"nextAction":"AWAIT_PARENT_CONFIRMATION"}""";
    private static string PageExample(string route)
    {
        var item = route.Contains("lessons", StringComparison.Ordinal) ? LessonExample
            : route.EndsWith("/members", StringComparison.Ordinal) ? """{"userId":"00000000-0000-0000-0000-000000000001","personId":null,"organizationPersonId":null,"displayName":"کاربر","username":"test.user","profilePhoto":null,"membershipId":"00000000-0000-0000-0000-000000000002","membershipStatus":"Active","activeRoles":["Teacher"],"roleAssignments":[{"roleAssignmentId":"00000000-0000-0000-0000-000000000003","role":"Teacher","status":"Active","assignedAtUtc":"2026-10-28T12:00:00Z","endedAtUtc":null,"version":"AAAAAAAAAAE="}],"version":"AAAAAAAAAAE="}"""
            : route.EndsWith("/learners", StringComparison.Ordinal) ? """{"enrollmentId":"00000000-0000-0000-0000-000000000001","learnerOrganizationPersonId":"00000000-0000-0000-0000-000000000002","personId":"00000000-0000-0000-0000-000000000003","userId":null,"displayName":"فراگیر","username":null,"profilePhoto":null,"status":"Active","enrolledAtUtc":"2026-10-28T12:00:00Z","endedAtUtc":null,"version":"AAAAAAAAAAE="}"""
            : route.EndsWith("/teachers", StringComparison.Ordinal) ? """{"teacherAssignmentId":"00000000-0000-0000-0000-000000000001","teacherMembershipId":"00000000-0000-0000-0000-000000000002","userId":"00000000-0000-0000-0000-000000000003","personId":null,"displayName":"مدرس","username":null,"profilePhoto":null,"status":"Active","assignedAtUtc":"2026-10-28T12:00:00Z","endedAtUtc":null,"version":"AAAAAAAAAAE="}"""
            : route.EndsWith("/periods", StringComparison.Ordinal) ? """{"id":"00000000-0000-0000-0000-000000000001","organizationId":"00000000-0000-0000-0000-000000000002","title":"دوره","startDate":"2026-09-01","endDate":"2027-06-30","status":"Active"}"""
            : """{"id":"00000000-0000-0000-0000-000000000001","organizationId":"00000000-0000-0000-0000-000000000002","title":"درس","status":"Active"}""";
        return "{\"dataAsOfUtc\":\"2026-10-28T12:00:00Z\",\"page\":1,\"pageSize\":50,\"totalCount\":1,\"items\":[" + item + "]}";
    }
}
