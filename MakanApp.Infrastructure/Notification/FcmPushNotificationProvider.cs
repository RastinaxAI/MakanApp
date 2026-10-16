using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Apis.Auth.OAuth2.Responses;
using MakanApp.Application.Notification;
using MakanApp.Domain.Notification;

namespace MakanApp.Infrastructure.Notification;

public sealed class FcmPushNotificationProvider(HttpClient httpClient, IFcmAccessTokenProvider tokenProvider,
    FcmTransportOptions options, TimeProvider timeProvider) : IPushNotificationProvider
{
    public bool IsConfigured => options.HasValidProjectSettings && tokenProvider.IsConfigured;

    public async Task<DeliveryProviderResult> SendAsync(PushNotificationDelivery delivery, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsConfigured) return options.HasConfiguration
            ? DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderConfigurationInvalid)
            : DeliveryProviderResult.Suppressed(NotificationDeliveryFailureReason.ProviderUnavailable);
        if (delivery.Platform is not (PushDevicePlatform.Android or PushDevicePlatform.Ios))
            return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderRejected);
        try
        {
            var token = await tokenProvider.GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token)) return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderAuthenticationFailed);
            var payload = PushWirePayload.Create(delivery);
            var data = new Dictionary<string, string>
            {
                ["notificationId"] = payload.NotificationId, ["type"] = payload.Type, ["deepLink"] = payload.DeepLink, ["tag"] = payload.Tag
            };
            if (payload.ConversationId is not null) data["conversationId"] = payload.ConversationId;
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{options.ProjectId}/messages:send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new
            {
                message = new
                {
                    token = delivery.PushToken, notification = new { title = payload.Title, body = payload.Body }, data,
                    android = new { ttl = $"{options.TimeToLiveSeconds}s", notification = new { tag = payload.Tag } },
                    apns = new
                    {
                        headers = new Dictionary<string, string>
                        {
                            ["apns-collapse-id"] = payload.Tag, ["apns-push-type"] = "alert",
                            ["apns-expiration"] = timeProvider.GetUtcNow().AddSeconds(options.TimeToLiveSeconds).ToUnixTimeSeconds()
                                .ToString(System.Globalization.CultureInfo.InvariantCulture)
                        },
                        payload = new { aps = new { alert = new { title = payload.Title, body = payload.Body } } }
                    }
                }
            });
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.RequestTimeout)
                return PushProviderHttpPolicy.Classify(response.StatusCode, response.Headers, timeProvider.GetUtcNow(), webPush: false);
            JsonDocument json;
            try { json = JsonDocument.Parse(await PushProviderHttpPolicy.ReadBoundedAsync(response.Content, cancellationToken)); }
            catch (JsonException) { return DeliveryProviderResult.Transient(NotificationDeliveryFailureReason.ProviderUnavailable); }
            using (json)
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return json.RootElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String &&
                        name.GetString() is { Length: > 0 and <= 200 } messageName && messageName.StartsWith($"projects/{options.ProjectId}/messages/", StringComparison.Ordinal)
                        ? DeliveryProviderResult.Delivered(messageName)
                        : DeliveryProviderResult.Transient(NotificationDeliveryFailureReason.ProviderUnavailable);
                }
                if (HasInvalidRegistrationError(json.RootElement))
                    return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.InvalidRegistration);
                return PushProviderHttpPolicy.Classify(response.StatusCode, response.Headers, timeProvider.GetUtcNow(), webPush: false);
            }
        }
        catch (TokenResponseException exception)
        {
            if (exception.StatusCode == HttpStatusCode.TooManyRequests)
                return DeliveryProviderResult.Transient(NotificationDeliveryFailureReason.ProviderRateLimited);
            return exception.StatusCode == HttpStatusCode.RequestTimeout || exception.StatusCode.HasValue && (int)exception.StatusCode.Value >= 500
                ? DeliveryProviderResult.Transient(NotificationDeliveryFailureReason.ProviderUnavailable)
                : DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderAuthenticationFailed);
        }
        catch (HttpRequestException) { return DeliveryProviderResult.Transient(NotificationDeliveryFailureReason.ProviderUnavailable); }
        catch (JsonException) { return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderConfigurationInvalid); }
        catch (Exception exception) when (exception is IOException or ArgumentException or FormatException or CryptographicException or InvalidOperationException or PlatformNotSupportedException)
        { return DeliveryProviderResult.Permanent(NotificationDeliveryFailureReason.ProviderConfigurationInvalid); }
    }

    private static bool HasInvalidRegistrationError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || !error.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.Array) return false;
        return details.EnumerateArray().Any(detail => detail.TryGetProperty("@type", out var type) &&
            type.GetString() == "type.googleapis.com/google.firebase.fcm.v1.FcmError" && detail.TryGetProperty("errorCode", out var code) &&
            code.GetString() is "UNREGISTERED" or "INVALID_ARGUMENT");
    }
}
