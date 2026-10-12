using MakanApp.Application.Messaging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class UnavailableContentSafetyProvider : IContentSafetyProvider
{
    public Task<ContentSafetyResult> EvaluateAsync(
        ContentSafetyRequest request,
        CancellationToken cancellationToken) =>
        throw new ContentSafetyProviderUnavailableException(
            "سرویس ایمنی محتوا در این محیط پیکربندی نشده است.");
}
