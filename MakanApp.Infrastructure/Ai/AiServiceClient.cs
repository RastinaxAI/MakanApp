using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Ai;

namespace MakanApp.Infrastructure.Ai;

public sealed class AiServiceClient(
    HttpClient httpClient,
    IAiServiceCredentialProvider credentialProvider,
    AiServiceOptions options,
    AiServiceCircuitBreaker circuitBreaker) : IAiServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public async Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        ValidateOptions();
        ValidateRequest(request);
        try
        {
            circuitBreaker.ThrowIfOpen();
        }
        catch (AiServiceException exception)
        {
            RecordFailure(exception.Code, started);
            throw;
        }

        AiServiceCredential credential;
        try
        {
            credential = await credentialProvider.GetCredentialAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            RecordFailure(AiServiceErrorCodes.ServiceUnavailable, started);
            throw new AiServiceException(
                AiServiceErrorCodes.ServiceUnavailable,
                "اعتبارنامه سرویس هوش مصنوعی در دسترس نیست.",
                innerException: exception);
        }
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.BaseUrl, UriKind.Absolute), "/api/v1/ai/chat"))
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(
            credential.Scheme,
            credential.Token);
        message.Headers.TryAddWithoutValidation("X-AI-Contract-Version", options.ContractVersion);
        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            message.Headers.TryAddWithoutValidation("X-Correlation-ID", request.CorrelationId);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            circuitBreaker.RecordTransientFailure();
            RecordFailure(AiServiceErrorCodes.ServiceTimeout, started);
            throw new AiServiceException(
                AiServiceErrorCodes.ServiceTimeout,
                "مهلت پاسخ‌گویی سرویس هوش مصنوعی تمام شد.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            circuitBreaker.RecordTransientFailure();
            RecordFailure(AiServiceErrorCodes.ServiceUnavailable, started);
            throw new AiServiceException(
                AiServiceErrorCodes.ServiceUnavailable,
                "سرویس هوش مصنوعی در دسترس نیست.",
                innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                ThrowMappedFailure(response, started);
            }

            try
            {
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType is null ||
                    !(mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                      mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)))
                {
                    throw InvalidResponse();
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<AiChatResponse>(
                    stream,
                    JsonOptions,
                    cancellationToken);
                ValidateResponse(request, result);
                circuitBreaker.RecordSuccess();
                AiServiceTelemetry.Record(
                    "success",
                    "none",
                    Stopwatch.GetElapsedTime(started));
                return result!;
            }
            catch (AiServiceException)
            {
                circuitBreaker.RecordTransientFailure();
                RecordFailure(AiServiceErrorCodes.ResponseInvalid, started);
                throw;
            }
            catch (JsonException exception)
            {
                circuitBreaker.RecordTransientFailure();
                RecordFailure(AiServiceErrorCodes.ResponseInvalid, started);
                throw InvalidResponse(exception);
            }
        }
    }

    private void ValidateOptions()
    {
        if (!options.Enabled)
        {
            throw new AiServiceException(
                AiServiceErrorCodes.FeatureDisabled,
                "قابلیت هوش مصنوعی فعال نیست.");
        }

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            options.TimeoutSeconds is < 1 or > 120 ||
            string.IsNullOrWhiteSpace(options.ContractVersion) ||
            options.CircuitBreakerFailureThreshold is < 1 or > 20 ||
            options.CircuitBreakerBreakSeconds is < 1 or > 300)
        {
            throw new InvalidOperationException("AiService configuration is invalid.");
        }
    }

    private static void ValidateRequest(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.Actor.UserId) ||
            request.Actor.Role is not ("Student" or "Parent" or "Teacher" or "Manager") ||
            string.IsNullOrWhiteSpace(request.Message.Text) ||
            string.IsNullOrWhiteSpace(request.Message.Language) ||
            request.Message.Language.Length > 6)
        {
            throw new AiServiceException(
                AiServiceErrorCodes.InvalidRequest,
                "درخواست هوش مصنوعی نامعتبر است.");
        }
    }

    private void ThrowMappedFailure(HttpResponseMessage response, long started)
    {
        var statusCode = (int)response.StatusCode;
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var code = response.StatusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                AiServiceErrorCodes.InvalidRequest,
            HttpStatusCode.Unauthorized => AiServiceErrorCodes.ServiceUnauthorized,
            HttpStatusCode.Forbidden => AiServiceErrorCodes.ServiceForbidden,
            HttpStatusCode.TooManyRequests => AiServiceErrorCodes.RateLimited,
            HttpStatusCode.GatewayTimeout => AiServiceErrorCodes.ServiceTimeout,
            _ => AiServiceErrorCodes.ServiceUnavailable
        };

        if (statusCode >= 500)
        {
            circuitBreaker.RecordTransientFailure();
        }

        RecordFailure(code, started);

        throw new AiServiceException(
            code,
            "سرویس هوش مصنوعی درخواست را تکمیل نکرد.",
            statusCode,
            retryAfter);
    }

    private static void ValidateResponse(AiChatRequest request, AiChatResponse? response)
    {
        if (response is null ||
            response.RequestId != request.RequestId ||
            response.CorrelationId != request.CorrelationId ||
            response.ConversationId != request.ConversationId ||
            string.IsNullOrWhiteSpace(response.Answer?.Text) ||
            string.IsNullOrWhiteSpace(response.Answer.Language) ||
            response.Insights is null || response.Risks is null ||
            response.Recommendations is null || response.Sources is null ||
            response.Metadata is null ||
            string.IsNullOrWhiteSpace(response.Metadata.ModelAlias) ||
            response.Metadata.ProcessingTimeMs < 0 ||
            response.Insights.Concat(response.Risks).Any(item =>
                string.IsNullOrWhiteSpace(item.Type) ||
                item.Confidence is < 0 or > 1) ||
            response.Recommendations.Any(item => string.IsNullOrWhiteSpace(item.Title)) ||
            response.Sources.Any(item =>
                string.IsNullOrWhiteSpace(item.Type) || string.IsNullOrWhiteSpace(item.Id)))
        {
            throw InvalidResponse();
        }
    }

    private static AiServiceException InvalidResponse(Exception? innerException = null) => new(
        AiServiceErrorCodes.ResponseInvalid,
        "پاسخ سرویس هوش مصنوعی معتبر نیست.",
        innerException: innerException);

    private static void RecordFailure(string code, long started) =>
        AiServiceTelemetry.Record("failure", code, Stopwatch.GetElapsedTime(started));
}
