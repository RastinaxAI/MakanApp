namespace MakanApp.Api.Observability;

// failure به host منتقل می‌شود، اما حتی stack تازهٔ مرز HTTP نیز مسیر source و جزئیات framework را افشا نمی‌کند.
public sealed class SanitizedRequestFailureException() : Exception("HTTP_PIPELINE_FAILED")
{
    public override string? StackTrace => null;
    public override string ToString() => Message;
}
