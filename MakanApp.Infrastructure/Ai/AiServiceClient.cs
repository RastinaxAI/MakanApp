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

    public Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message?.Text) ||
            string.IsNullOrWhiteSpace(request.Message.Language) ||
            request.Message.Language.Length > 6)
        {
            throw InvalidRequest();
        }

        return SendAsync<AiChatRequest, AiChatResponse>(
            "chat", "/api/v1/ai/chat", request, request.Actor.Role, ValidateChatResponse, cancellationToken);
    }

    public Task<AiStudentAnalysisResponse> AnalyzeStudentAsync(
        AiStudentAnalysisRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiStudentAnalysisRequest, AiStudentAnalysisResponse>(
            "student_analyze", "/api/v1/ai/students/analyze", request, "Student", ValidateStudentAnalysisResponse, cancellationToken);

    public Task<AiStudyPlanResponse> CreateStudyPlanAsync(
        AiStudyPlanRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiStudyPlanRequest, AiStudyPlanResponse>(
            "study_plan", "/api/v1/ai/study-plan", request, "Student", ValidateStudyPlanResponse, cancellationToken);

    public Task<AiHomeworkExplanationResponse> ExplainHomeworkAsync(
        AiHomeworkExplanationRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiHomeworkExplanationRequest, AiHomeworkExplanationResponse>(
            "homework_explain", "/api/v1/ai/homeworks/explain", request, "Student", ValidateHomeworkResponse, cancellationToken);

    public Task<AiExamPreparationResponse> PrepareExamAsync(
        AiExamPreparationRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiExamPreparationRequest, AiExamPreparationResponse>(
            "exam_prepare", "/api/v1/ai/exams/prepare", request, "Student", ValidateExamPreparationResponse, cancellationToken);

    public Task<AiParentChildSummaryResponse> SummarizeChildAsync(
        AiParentChildSummaryRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiParentChildSummaryRequest, AiParentChildSummaryResponse>(
            "parent_child_summary", "/api/v1/ai/parents/child-summary", request, "Parent", ValidateParentResponse, cancellationToken);

    public Task<AiRoleInsightResponse> SummarizeTeacherClassAsync(
        AiTeacherClassSummaryRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiTeacherClassSummaryRequest, AiRoleInsightResponse>(
            "teacher_class_summary", "/api/v1/ai/teachers/class-summary", request, "Teacher", ValidateRoleInsightResponse, cancellationToken);

    public Task<AiRoleInsightResponse> GetTeacherStudentInsightAsync(
        AiTeacherStudentInsightRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiTeacherStudentInsightRequest, AiRoleInsightResponse>(
            "teacher_student_insight", "/api/v1/ai/teachers/student-insight", request, "Teacher", ValidateRoleInsightResponse, cancellationToken);

    public Task<AiActivitySuggestionsResponse> SuggestTeacherActivitiesAsync(
        AiTeacherActivitySuggestionsRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiTeacherActivitySuggestionsRequest, AiActivitySuggestionsResponse>(
            "teacher_activity_suggestions", "/api/v1/ai/teachers/activity-suggestions", request, "Teacher", ValidateActivityResponse, cancellationToken);

    public Task<AiRoleInsightResponse> SummarizeManagerSchoolAsync(
        AiManagerSchoolSummaryRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiManagerSchoolSummaryRequest, AiRoleInsightResponse>(
            "manager_school_summary", "/api/v1/ai/managers/school-summary", request, "Manager", ValidateRoleInsightResponse, cancellationToken);

    public Task<AiRoleInsightResponse> GetManagerClassInsightAsync(
        AiManagerClassInsightRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiManagerClassInsightRequest, AiRoleInsightResponse>(
            "manager_class_insight", "/api/v1/ai/managers/class-insight", request, "Manager", ValidateRoleInsightResponse, cancellationToken);

    public Task<AiRoleInsightResponse> GetManagerStudentInsightAsync(
        AiManagerStudentInsightRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<AiManagerStudentInsightRequest, AiRoleInsightResponse>(
            "manager_student_insight", "/api/v1/ai/managers/student-insight", request, "Manager", ValidateRoleInsightResponse, cancellationToken);

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string operation,
        string path,
        TRequest request,
        string requiredRole,
        Action<TRequest, TResponse?> validateResponse,
        CancellationToken cancellationToken)
        where TRequest : IAiServiceRequest
        where TResponse : class, IAiServiceResponse
    {
        var started = Stopwatch.GetTimestamp();
        ValidateOptions();
        ValidateRequest(request, requiredRole);
        try
        {
            circuitBreaker.ThrowIfOpen();
        }
        catch (AiServiceException exception)
        {
            RecordFailure(operation, exception.Code, started);
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
            RecordFailure(operation, AiServiceErrorCodes.ServiceUnavailable, started);
            throw new AiServiceException(
                AiServiceErrorCodes.ServiceUnavailable,
                "اعتبارنامه سرویس هوش مصنوعی در دسترس نیست.",
                innerException: exception);
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(options.BaseUrl, UriKind.Absolute), path))
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(credential.Scheme, credential.Token);
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
            RecordFailure(operation, AiServiceErrorCodes.ServiceTimeout, started);
            throw new AiServiceException(
                AiServiceErrorCodes.ServiceTimeout,
                "مهلت پاسخ‌گویی سرویس هوش مصنوعی تمام شد.",
                innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            circuitBreaker.RecordTransientFailure();
            RecordFailure(operation, AiServiceErrorCodes.ServiceUnavailable, started);
            throw new AiServiceException(
                AiServiceErrorCodes.ServiceUnavailable,
                "سرویس هوش مصنوعی در دسترس نیست.",
                innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                ThrowMappedFailure(operation, response, started);
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
                var result = await JsonSerializer.DeserializeAsync<TResponse>(
                    stream,
                    JsonOptions,
                    cancellationToken);
                validateResponse(request, result);
                circuitBreaker.RecordSuccess();
                AiServiceTelemetry.Record(
                    operation,
                    "success",
                    "none",
                    Stopwatch.GetElapsedTime(started));
                return result!;
            }
            catch (AiServiceException)
            {
                circuitBreaker.RecordTransientFailure();
                RecordFailure(operation, AiServiceErrorCodes.ResponseInvalid, started);
                throw;
            }
            catch (JsonException exception)
            {
                circuitBreaker.RecordTransientFailure();
                RecordFailure(operation, AiServiceErrorCodes.ResponseInvalid, started);
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

    private static void ValidateRequest(IAiServiceRequest request, string requiredRole)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.Actor.UserId) ||
            request.Actor.Role != requiredRole ||
            request.RequestId.Length > 128 ||
            request.CorrelationId?.Length > 128)
        {
            throw InvalidRequest();
        }
    }

    private void ThrowMappedFailure(string operation, HttpResponseMessage response, long started)
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

        RecordFailure(operation, code, started);

        throw new AiServiceException(
            code,
            "سرویس هوش مصنوعی درخواست را تکمیل نکرد.",
            statusCode,
            retryAfter);
    }

    private static void ValidateChatResponse(AiChatRequest request, AiChatResponse? response)
    {
        ValidateEnvelope(request, response);
        if (response is null ||
            response.ConversationId != request.ConversationId ||
            string.IsNullOrWhiteSpace(response.Answer?.Text) ||
            string.IsNullOrWhiteSpace(response.Answer.Language) ||
            response.Insights is null || response.Risks is null ||
            response.Recommendations is null || response.Sources is null ||
            !ValidInsights(response.Insights.Concat(response.Risks)) ||
            !ValidRecommendations(response.Recommendations) ||
            !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateStudentAnalysisResponse(
        AiStudentAnalysisRequest request,
        AiStudentAnalysisResponse? response)
    {
        ValidateEnvelope(request, response);
        if (response is null || string.IsNullOrWhiteSpace(response.Summary) ||
            response.Strengths is null || response.Weaknesses is null || response.Risks is null ||
            response.Recommendations is null || response.Sources is null ||
            !ValidInsights(response.Strengths.Concat(response.Weaknesses).Concat(response.Risks)) ||
            !ValidRecommendations(response.Recommendations) || !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateStudyPlanResponse(AiStudyPlanRequest request, AiStudyPlanResponse? response)
    {
        ValidateEnvelope(request, response);
        if (response is null || string.IsNullOrWhiteSpace(response.Summary) ||
            response.Priorities is null || response.PlanItems is null ||
            response.Recommendations is null || response.Sources is null ||
            response.Priorities.Any(string.IsNullOrWhiteSpace) ||
            response.PlanItems.Any(item =>
                string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Reason) ||
                item.EstimatedMinutes is <= 0) ||
            !ValidRecommendations(response.Recommendations) || !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateHomeworkResponse(
        AiHomeworkExplanationRequest request,
        AiHomeworkExplanationResponse? response)
    {
        ValidateEnvelope(request, response);
        if (response is null || string.IsNullOrWhiteSpace(response.Explanation) ||
            response.Steps is null || response.Hints is null ||
            response.Recommendations is null || response.Sources is null ||
            response.Steps.Any(string.IsNullOrWhiteSpace) || response.Hints.Any(string.IsNullOrWhiteSpace) ||
            !ValidRecommendations(response.Recommendations) || !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateExamPreparationResponse(
        AiExamPreparationRequest request,
        AiExamPreparationResponse? response)
    {
        ValidateEnvelope(request, response);
        if (response is null || string.IsNullOrWhiteSpace(response.Summary) ||
            response.PriorityTopics is null || response.PreparationSteps is null ||
            response.Risks is null || response.Recommendations is null || response.Sources is null ||
            response.PriorityTopics.Any(string.IsNullOrWhiteSpace) ||
            response.PreparationSteps.Any(string.IsNullOrWhiteSpace) ||
            !ValidInsights(response.Risks) ||
            !ValidRecommendations(response.Recommendations) || !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateParentResponse(
        AiParentChildSummaryRequest request,
        AiParentChildSummaryResponse? response)
    {
        ValidateEnvelope(request, response);
        if (response is null || string.IsNullOrWhiteSpace(response.AcademicSummary) ||
            response.Progress is null || string.IsNullOrWhiteSpace(response.Progress.Description) ||
            response.AttentionAreas is null || response.UpcomingItems is null ||
            response.Risks is null || response.Recommendations is null || response.Sources is null ||
            response.Progress.Trend is not ("Improving" or "Stable" or "Declining" or "Unknown") ||
            response.AttentionAreas.Any(item =>
                string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Description)) ||
            response.UpcomingItems.Any(item =>
                item.Type is not ("Homework" or "Exam") ||
                string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title)) ||
            !ValidInsights(response.Risks) ||
            !ValidRecommendations(response.Recommendations) || !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateRoleInsightResponse<TRequest>(
        TRequest request,
        AiRoleInsightResponse? response)
        where TRequest : IAiServiceRequest
    {
        ValidateEnvelope(request, response);
        if (response is null || string.IsNullOrWhiteSpace(response.Summary) ||
            response.Strengths is null || response.AttentionAreas is null ||
            response.Risks is null || response.Recommendations is null || response.Sources is null ||
            !ValidInsights(response.Strengths.Concat(response.AttentionAreas).Concat(response.Risks)) ||
            !ValidRecommendations(response.Recommendations) || !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateActivityResponse(
        AiTeacherActivitySuggestionsRequest request,
        AiActivitySuggestionsResponse? response)
    {
        ValidateEnvelope(request, response);
        if (response is null || string.IsNullOrWhiteSpace(response.Summary) ||
            response.Activities is null || response.Recommendations is null || response.Sources is null ||
            response.Activities.Any(item =>
                string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Objective) ||
                string.IsNullOrWhiteSpace(item.Reason) ||
                item.Steps is null || item.Steps.Any(string.IsNullOrWhiteSpace) ||
                item.EstimatedMinutes is <= 0) ||
            !ValidRecommendations(response.Recommendations) || !ValidSources(response.Sources))
        {
            throw InvalidResponse();
        }
    }

    private static void ValidateEnvelope<TRequest, TResponse>(TRequest request, TResponse? response)
        where TRequest : IAiServiceRequest
        where TResponse : class, IAiServiceResponse
    {
        if (response is null ||
            response.RequestId != request.RequestId ||
            response.CorrelationId != request.CorrelationId ||
            response.Metadata is null ||
            string.IsNullOrWhiteSpace(response.Metadata.ModelAlias) ||
            response.Metadata.ProcessingTimeMs < 0)
        {
            throw InvalidResponse();
        }
    }

    private static AiServiceException InvalidResponse(Exception? innerException = null) => new(
        AiServiceErrorCodes.ResponseInvalid,
        "پاسخ سرویس هوش مصنوعی معتبر نیست.",
        innerException: innerException);

    private static AiServiceException InvalidRequest() => new(
        AiServiceErrorCodes.InvalidRequest,
        "درخواست هوش مصنوعی نامعتبر است.");

    private static bool ValidInsights(IEnumerable<AiInsight> values) =>
        values.All(item =>
            !string.IsNullOrWhiteSpace(item.Type) &&
            item.Confidence is not (< 0 or > 1));

    private static bool ValidRecommendations(IEnumerable<AiRecommendation> values) =>
        values.All(item => !string.IsNullOrWhiteSpace(item.Title));

    private static bool ValidSources(IEnumerable<AiSourceReference> values) =>
        values.All(item =>
            !string.IsNullOrWhiteSpace(item.Type) && !string.IsNullOrWhiteSpace(item.Id));

    private static void RecordFailure(string operation, string code, long started) =>
        AiServiceTelemetry.Record(operation, "failure", code, Stopwatch.GetElapsedTime(started));
}
