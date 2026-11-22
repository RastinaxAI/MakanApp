using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;

namespace MakanApp.Infrastructure.Messaging;

// موتور متن اسکنر فایل نیست؛ هر رسانه به تصمیم مستقل و ثبت‌شدهٔ Provider رسانه نیاز دارد.
public sealed class ReviewedMediaContentSafetyProvider(InternalMakanContentSafetyProvider textProvider,
    IMediaSafetyProvider mediaProvider) : IContentSafetyProvider
{
    public async Task<ContentSafetyResult> EvaluateAsync(ContentSafetyRequest request, CancellationToken ct)
    {
        if (request.Attachments.Count == 0 || request.Kind is not (MessageKind.File or MessageKind.Image))
            return await textProvider.EvaluateAsync(request, ct);
        var caption = await textProvider.EvaluateAsync(request with { Kind = MessageKind.Text, Attachments = [] }, ct);
        if (caption.Decision != ContentSafetyDecision.Allowed) return caption;
        var decision = ContentSafetyDecision.Allowed;
        foreach (var attachment in request.Attachments)
        {
            var result = await mediaProvider.EvaluateAsync(attachment, ct);
            if (!Enum.IsDefined(result.Decision)) throw new ContentSafetyProviderUnavailableException("نتیجهٔ بررسی رسانه معتبر نیست.");
            if (result.Decision == ContentSafetyDecision.Unavailable) return new(ContentSafetyDecision.Unavailable);
            if (result.Decision == ContentSafetyDecision.Blocked) decision = ContentSafetyDecision.Blocked;
            else if (result.Decision == ContentSafetyDecision.ReviewRequired && decision != ContentSafetyDecision.Blocked)
                decision = ContentSafetyDecision.ReviewRequired;
        }
        return new(decision, decision == ContentSafetyDecision.Blocked ? "High" : decision == ContentSafetyDecision.Allowed ? "Low" : "Review", null,
            [$"{ManualMediaSafetyProvider.PolicyVersion}:{decision}"], null, ManualMediaSafetyProvider.PolicyVersion,
            null, UsesMediaReview: true);
    }
}
