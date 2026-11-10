using MakanApp.Application.Commerce;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace MakanApp.Api.OpenApi;

public sealed class CommercePaymentOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = context.Description.RelativePath?.Split('?')[0] ?? "";
        if (!route.StartsWith("api/v1/commerce/", StringComparison.Ordinal) && !route.StartsWith("api/integrations/v1/commerce/payments/", StringComparison.Ordinal))
            return Task.CompletedTask;
        var callback = route.EndsWith("zibal/callback", StringComparison.Ordinal);
        operation.Description = (operation.Description ?? "") + (callback
            ? "\nCallback عمومی GET فقط محرک Verify سمت سرور است؛ success/status مجوز تسویه نیستند. orderId شناسهٔ N-format تلاش پرداخت است. حتی callback لغو بررسی می‌شود. پاسخ 303 فقط شناسه‌ها را به ClientReturnUrl ثابت و allowlisted برمی‌گرداند؛ کلاینت باید API وضعیتِ دارای نشست را بخواند."
            : "\nنشست معتبر و مجوز جاری لازم است. سفارش شخصی فقط برای خریدار و سفارش سازمانی برای Manager سازمانِ Workspace جاری مجاز است. مبلغ ریال و شرایط از سفارش frozen می‌آیند. ProviderReference شناسهٔ trackId است و SettlementReference مرجع refNumber. وضعیت Order، PaymentAttempt و Fulfillment مستقل‌اند. Recovery فقط شناسهٔ تلاش را می‌پذیرد؛ Retry تسویه یا تحویل تکراری نمی‌سازد. تاریخچه حداکثر ۱۰۰ رخداد اخیر است و حذف نمی‌شود.");
        if (callback)
        {
            operation.Summary = "بازگشت زیبال و بررسی معتبر پرداخت";
            operation.Parameters = new List<OpenApiParameter>
            {
                Query("orderId", "string", "شناسهٔ PaymentAttempt با ۳۲ نویسهٔ N-format؛ Order.Id نیست."),
                Query("trackId", "string", "Int64 مثبت به‌شکل رشتهٔ ده‌دهی؛ با تلاش ثبت‌شده تطبیق داده می‌شود."),
                Query("success", "string", "فقط 0 یا 1؛ hint غیرقابل اعتماد."),
                Query("status", "integer", "وضعیت گزارش‌شدهٔ callback؛ اثبات پرداخت نیست.")
            };
            operation.Responses.Remove("200");
            operation.Responses["303"] = new OpenApiResponse { Description = "بازگشت به آدرس ثابت؛ بدون ادعای موفقیت پرداخت.",
                Headers = new Dictionary<string, OpenApiHeader> { ["Location"] = new() { Description = "آدرس allowlisted با orderId و paymentAttemptId", Schema = new() { Type = "string", Format = "uri" } } } };
        }
        Error(operation, "400", CommerceErrorCodes.PaymentInvalid, CommerceErrorCodes.PaymentReferenceInvalid);
        Error(operation, "404", CommerceErrorCodes.PaymentAttemptNotFound, CommerceErrorCodes.OrderNotFound);
        Error(operation, "409", CommerceErrorCodes.PaymentPendingVerification, CommerceErrorCodes.PaymentReferenceConflict,
            CommerceErrorCodes.OrderNotPayable, CommerceErrorCodes.PaymentIdempotencyConflict, CommerceErrorCodes.OrderIdempotencyConflict);
        Error(operation, "503", CommerceErrorCodes.PaymentProviderUnavailable);
        if (context.Description.HttpMethod == "POST" || callback) Error(operation, "429", CommerceErrorCodes.PaymentRateLimited);
        return Task.CompletedTask;
    }
    private static OpenApiParameter Query(string name, string type, string description) => new()
        { Name = name, In = ParameterLocation.Query, Required = true, Description = description, Schema = new() { Type = type } };
    private static void Error(OpenApiOperation operation, string status, params string[] codes) =>
        operation.Responses[status] = new OpenApiResponse { Description = string.Join("، ", codes),
            Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() { Schema = ApiContractDocumentTransformer.ProblemReference() } } };
}
