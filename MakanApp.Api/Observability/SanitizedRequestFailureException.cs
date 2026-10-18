namespace MakanApp.Api.Observability;

// failure به host منتقل می‌شود، اما پیام، inner و stack اصلیِ حاوی دادهٔ خصوصی منتقل نمی‌شوند.
public sealed class SanitizedRequestFailureException() : Exception("HTTP_PIPELINE_FAILED");
