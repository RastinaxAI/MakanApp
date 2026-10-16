using System.Net;
using System.Net.Http.Headers;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;

namespace MakanApp.Infrastructure.Notification;

public static class PushProviderHttpPolicy
{
    public static DeliveryProviderResult Classify(HttpStatusCode status, HttpResponseHeaders headers, DateTimeOffset now,
        bool webPush)
    {
        if (webPush && status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.InvalidRegistration);
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderAuthenticationFailed);
        if ((int)status == 429 || (int)status >= 500 || status == HttpStatusCode.RequestTimeout)
        {
            var retryAfter = headers.RetryAfter?.Delta ?? (headers.RetryAfter?.Date - now);
            // مهلت بسیار بزرگ/نامعتبر باعث overflow یا retry زودتر از خواست provider نمی‌شود.
            if (retryAfter > TimeSpan.FromDays(1))
                return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderRejected);
            return DeliveryProviderResult.Transient((int)status == 429
                ? NotificationDeliveryFailureReason.ProviderRateLimited : NotificationDeliveryFailureReason.ProviderUnavailable,
                retryAfter > TimeSpan.Zero ? retryAfter : null);
        }
        return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderConfigurationInvalid);
    }

    public static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        const int limit = 16_384;
        if (content.Headers.ContentLength > limit) throw new InvalidDataException("پاسخ Push بیش از حد مجاز است.");
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[limit + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count), cancellationToken);
            if (read == 0) break;
            count += read;
        }
        if (count > limit) throw new InvalidDataException("پاسخ Push بیش از حد مجاز است.");
        return buffer[..count];
    }
}
