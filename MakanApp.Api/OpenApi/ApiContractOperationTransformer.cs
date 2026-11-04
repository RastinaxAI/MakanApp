using System.Reflection;
using System.Text.RegularExpressions;
using MakanApp.Api.Errors;
using MakanApp.Application.Academic;
using MakanApp.Application.Assessment;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class ApiContractOperationTransformer(StorageOptions storage) : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? "";
        var method = context.Description.HttpMethod ?? "GET";
        // شناسه از قرارداد HTTP ساخته می‌شود؛ نام داخلی متد یا ترتیب کشف endpoint در آن اثری ندارد.
        operation.OperationId = method.ToLowerInvariant() + "_" + Regex.Replace(route.Replace("{", "by_", StringComparison.Ordinal).Replace("}", "", StringComparison.Ordinal), "[^A-Za-z0-9]+", "_").Trim('_');
        var metadata = context.Description.ActionDescriptor.EndpointMetadata ?? [];
        var authorizations = metadata.OfType<IAuthorizeData>().ToArray();
        var anonymous = metadata.OfType<IAllowAnonymous>().Any() || authorizations.Length == 0;
        // مدل‌نویس فعلی آرایهٔ خالی را حذف می‌کند؛ requirement خالی، دسترسی anonymous را صریح نگه می‌دارد.
        operation.Security = anonymous ? [new OpenApiSecurityRequirement()] : [new OpenApiSecurityRequirement
        { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() }];
        operation.Extensions["x-authentication"] = new OpenApiString(anonymous ? "anonymous" : "bearer-session");
        var permissions = authorizations.Select(a => a.Policy).Where(p => p is not null).Distinct().Order().ToArray();
        if (permissions.Length > 0)
        {
            var values = new OpenApiArray(); foreach (var permission in permissions) values.Add(new OpenApiString(permission));
            operation.Extensions["x-platform-permissions"] = values;
            operation.Description = (operation.Description ?? "") + "\nنشست معتبر Backend و مجوزهای مستقل و جاری Platform لازم‌اند: " + string.Join(", ", permissions) + ". نشست معتبر به‌تنهایی مجوز Platform ایجاد نمی‌کند؛ permission جاری در هر درخواست دوباره سمت سرور بررسی می‌شود.";
        }
        if (!anonymous)
        {
            Error(operation, "401", [IdentityErrorCodes.AuthRequired]);
            Error(operation, "403", [ApiErrorCodes.Forbidden]);
        }
        var target = route.Contains("personal-class", StringComparison.Ordinal) || route.StartsWith("api/v1/academic/", StringComparison.Ordinal);
        if (target)
        {
            var action = (context.Description.ActionDescriptor as ControllerActionDescriptor)?.ActionName ?? method;
            operation.Summary ??= "عملیات " + action + " در قرارداد " + (route.Contains("personal-class", StringComparison.Ordinal) ? "کلاس مستقل" : "آموزشی مشترک");
            operation.Description ??= "نشست و مجوز جاری منبع لازم است. شناسهٔ ورودی، اثبات دسترسی نیست؛ HTTP 404 می‌تواند برای پنهان‌سازی منبع نامجاز برگردد.";
            var types = route.StartsWith("api/v1/academic/", StringComparison.Ordinal)
                ? new[] { typeof(AssessmentErrorCodes), typeof(AcademicErrorCodes), typeof(StorageErrorCodes), typeof(CommerceErrorCodes) }
                : new[] { typeof(AcademicErrorCodes), typeof(CommerceErrorCodes), typeof(PlatformErrorCodes) };
            var catalog = types.SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
                .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!).Distinct()
                // خطاهای شناخته‌شدهٔ بدون نگاشت اختصاصی، همان fallback واقعی handler یعنی 400 را دارند.
                .Select(code => new { Code = code, Status = GlobalExceptionHandler.GetKnownStatusCode(code) ?? StatusCodes.Status400BadRequest })
                .GroupBy(x => x.Status);
            foreach (var family in catalog) Error(operation, family.Key.ToString(System.Globalization.CultureInfo.InvariantCulture), family.Select(x => x.Code).Order().ToArray());
            Error(operation, "400", [ApiErrorCodes.InvalidRequest]);
            DescribeParameters(operation, route);
        }
        if (route.StartsWith("api/admin/v1/", StringComparison.Ordinal) && method is not ("GET" or "HEAD" or "OPTIONS"))
            Error(operation, "429", [ApiErrorCodes.RateLimited]);
        if (route.StartsWith("api/v1/organization/members/{membershipId}", StringComparison.Ordinal))
        {
            operation.Summary = method switch
            {
                "POST" => "اعطای نقش به عضویت موجود همان سازمان",
                "PUT" => "پایان دورهٔ نقش فعلی و ایجاد یا استفاده از نقش مقصد",
                _ => route.EndsWith("{roleAssignmentId}", StringComparison.Ordinal) ? "پایان دورهٔ نقش عضویت" : "پایان عضویت و نقش‌های فعال آن"
            };
            operation.Description = "Manager جاری سازمان می‌تواند Teacher/Student/Parent/Manager را مدیریت کند؛ حذف یا تغییر آخرین مدیر فعال ممنوع است. " +
                "عضویت تازه از این مسیر ساخته نمی‌شود. نسخهٔ membership و دورهٔ role مستقل‌اند. ClientOperationId در scope سازمان+عامل یکتاست؛ " +
                "Retry همان payload رسید قبلی و وضعیت جاری را بدون mutation تکراری می‌دهد. Replay نیز مجوز جاری می‌خواهد. " +
                "تاریخچه، User، Enrollment، TeacherAssignment و فایل شخصی حذف نمی‌شوند؛ دسترسی آینده با رابطه و نقش جاری بازبینی می‌شود.";
            Error(operation, "400", [ApiErrorCodes.InvalidRequest, MakanApp.Application.Organization.SchoolOnboardingErrorCodes.Invalid]);
            Error(operation, "403", [MakanApp.Application.Organization.OrganizationErrorCodes.WorkspaceNotAllowed]);
            Error(operation, "403", [CommerceErrorCodes.FeatureNotEntitled]);
            Error(operation, "404", [MakanApp.Application.Organization.SchoolMembershipErrorCodes.NotFound, MakanApp.Application.Organization.SchoolMembershipErrorCodes.RoleNotFound]);
            Error(operation, "409", [MakanApp.Application.Organization.SchoolMembershipErrorCodes.Conflict, MakanApp.Application.Organization.SchoolMembershipErrorCodes.OperationConflict,
                MakanApp.Application.Organization.SchoolMembershipErrorCodes.LastManagerRequired]);
            Error(operation, "412", [MakanApp.Application.Organization.OrganizationErrorCodes.ConcurrencyConflict]);
            Error(operation, "409", [CommerceErrorCodes.CommercialLocked, CommerceErrorCodes.QuotaExceeded]);
        }
        var gapRead = route is "api/admin/v1/provisioning" or "api/admin/v1/personal-classes/{classId}/memberships" or
            "api/v1/organization/invitations" or "api/v1/organization/teacher-content-activities" or "api/v1/academic/exams/discovery";
        if (gapRead && method == "GET")
        {
            Error(operation, "400", [ApiErrorCodes.InvalidRequest]);
            operation.Summary = route switch
            {
                "api/admin/v1/provisioning" => "فهرست و lookup دقیق قصد سازمان‌سازی",
                "api/admin/v1/personal-classes/{classId}/memberships" => "خواندن دوره‌های عضویت کلاس مستقل با نسخهٔ همان دوره",
                "api/v1/organization/invitations" => "دعوت‌های ارسالی سازمان جاری با وضعیت و انقضای مؤثر",
                "api/v1/organization/teacher-content-activities" => "گزارش نسخه‌های محتوای منتشرشدهٔ سازمان؛ بدون بدنه یا یادداشت خصوصی",
                _ => "کشف صفحه‌بندی‌شدهٔ نسخهٔ جاری آزمون کلاس‌های مجاز مدرس یا مدیر"
            };
            operation.Description = (operation.Description ?? "") + (route switch
            {
                "api/admin/v1/provisioning" => "\nclientOperationId اختیاری، string و حداکثر ۱۰۰ نویسه است؛ query پیش از paging اعمال می‌شود. unknown=صفحهٔ خالی ۲۰۰؛ GET عملیات را Retry نمی‌کند.",
                "api/admin/v1/personal-classes/{classId}/memberships" => "\nPersonalClassesRead مجوز این metadata است. membershipVersion نسخهٔ همان دوره است، نه Grant؛ Grant مستقیم Platform فقط برای Manager جاری است و Teacher از Manager تفویض می‌گیرد.",
                "api/v1/organization/invitations" => "\nفقط Manager با Workspace سازمانی جاری؛ OrganizationId ورودی ندارد. نام در context مجاز است، شمارهٔ کامل برگردانده نمی‌شود. Revoke فقط برای نوع/وضعیت دارای مسیر موجود پیشنهاد می‌شود.",
                "api/v1/organization/teacher-content-activities" => "\nفقط Manager همان سازمان؛ منابع Published واقعی، نویسندهٔ نسخه و بازهٔ نیمه‌باز UTC. eventId برای منبع تاریخی بدون رویداد null است؛ GET رویداد نمی‌سازد.",
                _ => "\nManager یا Teacher با انتساب جاری همان کلاس در سازمانِ Workspace؛ StudentExamSummary بدون Editor/Questions/AnswerKey. endpoint آرایه‌ای قبلی حفظ شده است."
            });
            if (route.StartsWith("api/v1/", StringComparison.Ordinal))
            {
                Error(operation, "400", [MakanApp.Application.Organization.SchoolOnboardingErrorCodes.Invalid, SchoolErrorCodes.Invalid]);
                Error(operation, "403", [MakanApp.Application.Organization.OrganizationErrorCodes.WorkspaceNotAllowed, AcademicErrorCodes.AcademicReadNotAllowed]);
            }
            foreach (var p in operation.Parameters ?? [])
            {
                var metadataParameter = context.Description.ParameterDescriptions.SingleOrDefault(d =>
                    string.Equals(d.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                var parameterType = metadataParameter?.ModelMetadata?.ModelType ?? metadataParameter?.Type;
                var enumType = parameterType is null ? null : Nullable.GetUnderlyingType(parameterType) ?? parameterType;
                if (p.In == ParameterLocation.Query) p.Name = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(p.Name);
                if (enumType?.IsEnum == true && !enumType.IsDefined(typeof(FlagsAttribute), false))
                    p.Schema.Enum = Enum.GetNames(enumType).Select(n => (IOpenApiAny)new OpenApiString(n)).ToList();
                if (enumType?.IsEnum == true && enumType.IsDefined(typeof(FlagsAttribute), false))
                    p.Description = "نقش یا ترکیب نقش‌های معتبر با comma؛ None یا بیت ناشناخته برای فیلتر پذیرفته نمی‌شود.";
                if (p.Name == "page") { p.Schema.Minimum = 1; p.Schema.Maximum = route.Contains("personal-classes", StringComparison.Ordinal) ? 1_000_000 : 100_000; p.Schema.Default = new OpenApiInteger(1); }
                if (p.Name == "pageSize") { p.Schema.Minimum = 1; p.Schema.Maximum = 100; p.Schema.Default = new OpenApiInteger(route.StartsWith("api/admin/", StringComparison.Ordinal) ? 30 : route.EndsWith("/discovery", StringComparison.Ordinal) ? 25 : 50); }
                if (p.Name == "search") p.Schema.MaxLength = route.Contains("personal-classes", StringComparison.Ordinal) ? 100 : 200;
                if (p.Name == "clientOperationId")
                { p.Schema.MaxLength = 100; p.Schema.MinLength = 1; p.Description = "شناسهٔ string همان قصد؛ trim و exact lookup با semantics مقایسهٔ SQL موجود. نبود نتیجه: صفحهٔ خالی ۲۰۰؛ این GET عملیات را Retry نمی‌کند."; }
            }
        }
        if (route == "api/v1/academic/classes/{classId}" && method == "PATCH")
            Error(operation, "400", [MakanApp.Application.Organization.SchoolOnboardingErrorCodes.Invalid]);
        if (route.EndsWith("official-spaces/activities", StringComparison.Ordinal) && method == "GET")
        {
            Error(operation, "400", [MakanApp.Application.Messaging.MessagingErrorCodes.AcademicActivityQueryInvalid]);
            Error(operation, "404", [MakanApp.Application.Messaging.MessagingErrorCodes.ConversationNotFound, MakanApp.Application.Messaging.MessagingErrorCodes.AcademicActivityPublicationMissing]);
            var revision = operation.Parameters?.SingleOrDefault(p => p.Name == "revision");
            if (revision is not null) { revision.Required = false; revision.Schema.Minimum = 1; revision.Description = "اختیاری؛ در نبود مقدار، جدیدترین رویداد ثبت‌شدهٔ همان منبع مجاز خوانده می‌شود. از publicationReceipt مقدار دقیق را بگیرید؛ GET هیچ رویدادی نمی‌سازد."; }
        }
        if (method == "POST" && route == "api/v1/files" && operation.RequestBody?.Content.TryGetValue("multipart/form-data", out var multipart) == true)
        {
            operation.RequestBody.Required = true;
            multipart.Schema.Required ??= new HashSet<string>(); multipart.Schema.Required.Add("File");
            if (multipart.Schema.Properties.TryGetValue("File", out var file))
            {
                file.Type = "string"; file.Format = "binary";
                file.Description = "بخش File اجباری است؛ سقف هر فایل از Storage می‌آید و محدودیت تعداد/حجم پاسخ از policy فعالیت نیز بررسی می‌شود.";
                file.Extensions["x-max-file-size-bytes"] = new OpenApiLong(storage.MaxFileSizeBytes);
                var mimes = new OpenApiArray(); foreach (var mime in storage.AllowedContentTypes) mimes.Add(new OpenApiString(mime));
                file.Extensions["x-allowed-content-types"] = mimes;
            }
        }
        var binary = method == "GET" && (route.EndsWith("/content", StringComparison.Ordinal) &&
            (route.StartsWith("api/v1/sources/", StringComparison.Ordinal) || route.StartsWith("api/v1/reports/exports/", StringComparison.Ordinal)) ||
            route.Contains("/evidence/files/", StringComparison.Ordinal) || route.StartsWith("api/v1/reports/exports/", StringComparison.Ordinal) && route.EndsWith("/download", StringComparison.Ordinal));
        if (binary)
        {
            operation.Responses["200"] = FileAndMessagingOperationTransformer.Binary("بایت خام فایل؛ Content-Type واقعی از metadata معتبر فایل می‌آید.");
            if (!route.StartsWith("api/v1/reports/", StringComparison.Ordinal))
            {
                operation.Responses["206"] = FileAndMessagingOperationTransformer.Binary("بازهٔ معتبر فایل روی stream قابل seek؛ Content-Range را بخوانید.");
                operation.Responses["206"].Headers["Content-Range"] = new OpenApiHeader
                    { Description = "بازهٔ واقعی و اندازهٔ کل فایل", Schema = new OpenApiSchema { Type = "string" } };
                operation.Responses["416"] = new OpenApiResponse { Description = "بازهٔ درخواستی معتبر نیست؛ پاسخ فایل موفق نیست و ممکن است بدنه نداشته باشد." };
            }
        }
        // schemaهای اختصاصی خطا، مانند Safety، با قرارداد پایه ترکیب می‌شوند و از بین نمی‌روند.
        foreach (var response in operation.Responses.Where(p => int.TryParse(p.Key, out var status) && status >= 400 && status != 416))
        {
            response.Value.Content.TryAdd("application/problem+json", new OpenApiMediaType());
            var media = response.Value.Content["application/problem+json"];
            if (media.Schema is { Properties.Count: > 0 } existing)
            {
                existing.AllOf ??= new List<OpenApiSchema>();
                if (!existing.AllOf.Any(s => s.Reference?.Id == "MakanProblemDetails")) existing.AllOf.Add(ApiContractDocumentTransformer.ProblemReference());
            }
            else media.Schema = ApiContractDocumentTransformer.ProblemReference();
        }
        return Task.CompletedTask;
    }

    private static void Error(OpenApiOperation operation, string status, IReadOnlyCollection<string> codes)
    {
        operation.Responses.TryAdd(status, new OpenApiResponse { Description = "ProblemDetails؛ خانوادهٔ خطاهای مرتبط این حوزه، نه تضمین وقوع همهٔ کدها در هر عملیات." });
        var response = operation.Responses[status];
        var values = response.Extensions.TryGetValue("x-error-codes", out var existing) && existing is OpenApiArray list ? list : new OpenApiArray();
        foreach (var code in codes) if (!values.OfType<OpenApiString>().Any(v => v.Value == code)) values.Add(new OpenApiString(code));
        response.Extensions["x-error-codes"] = values;
        response.Content.TryAdd("application/problem+json", new OpenApiMediaType { Schema = ApiContractDocumentTransformer.ProblemReference() });
    }

    private static void DescribeParameters(OpenApiOperation operation, string route)
    {
        foreach (var p in operation.Parameters ?? [])
        {
            if (p.Name == "page") { p.Schema.Minimum = 1; p.Schema.Maximum = route.Contains("personal-class", StringComparison.Ordinal) && !route.StartsWith("api/v1/academic/", StringComparison.Ordinal) ? 1_000_000 : 100_000; p.Schema.Default = new OpenApiInteger(1); }
            if (p.Name == "pageSize") { p.Schema.Minimum = 1; p.Schema.Maximum = 100; p.Schema.Default = new OpenApiInteger(route.Contains("personal-class", StringComparison.Ordinal) && !route.Contains("/academic/", StringComparison.Ordinal) ? 30 : 25); }
            if (p.Name == "search") { p.Schema.MaxLength = 200; p.Description = "جست‌وجوی trimشده؛ فقط دادهٔ مجاز همان context."; }
            if (p.Name.Contains("Version", StringComparison.OrdinalIgnoreCase) && p.Schema.Type == "string")
            { p.Schema.Format = "byte"; p.Description = "Base64 استاندارد rowversion هشت‌بایتی؛ نسخهٔ کهنه conflict دارد."; }
        }
    }
}
