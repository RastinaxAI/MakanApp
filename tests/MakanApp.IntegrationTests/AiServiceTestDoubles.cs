using MakanApp.Application.Ai;

namespace MakanApp.IntegrationTests;

public sealed class AiServiceTestControl
{
    private readonly object _sync = new();
    private AiChatRequest? _lastRequest;
    private IAiServiceRequest? _lastRoleRequest;
    private Exception? _nextException;
    public int ProviderCallCount { get; private set; }

    public AiChatRequest? LastRequest
    {
        get
        {
            lock (_sync)
            {
                return _lastRequest;
            }
        }
    }

    public IAiServiceRequest? LastRoleRequest
    {
        get
        {
            lock (_sync)
            {
                return _lastRoleRequest;
            }
        }
    }

    public void FailNext(Exception exception)
    {
        lock (_sync)
        {
            _nextException = exception;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _lastRequest = null;
            _lastRoleRequest = null;
            _nextException = null;
            ProviderCallCount = 0;
        }
    }

    internal AiChatResponse Execute(AiChatRequest request)
    {
        lock (_sync)
        {
            _lastRequest = request;
            ProviderCallCount++;
            if (_nextException is not null)
            {
                var exception = _nextException;
                _nextException = null;
                throw exception;
            }
        }

        return new AiChatResponse(
            request.RequestId,
            request.CorrelationId,
            request.ConversationId,
            new AiMessage("پاسخ آزمایشی"),
            [],
            [],
            [],
            [],
            new AiResponseMetadata("integration-test", 1));
    }

    internal void Capture(IAiServiceRequest request)
    {
        lock (_sync)
        {
            _lastRoleRequest = request;
            ProviderCallCount++;
            ThrowPendingFailure();
        }
    }

    private void ThrowPendingFailure()
    {
        if (_nextException is null)
        {
            return;
        }

        var exception = _nextException;
        _nextException = null;
        throw exception;
    }
}

public sealed class RecordingAiServiceClient(AiServiceTestControl control) : IAiServiceClient
{
    public Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(control.Execute(request));
    }

    public Task<AiStudentAnalysisResponse> AnalyzeStudentAsync(AiStudentAnalysisRequest request, CancellationToken cancellationToken)
    {
        Capture(request, cancellationToken);
        return Task.FromResult(new AiStudentAnalysisResponse(
            request.RequestId, request.CorrelationId, "خلاصه", [], [], [], [], [], Metadata()));
    }

    public Task<AiStudyPlanResponse> CreateStudyPlanAsync(AiStudyPlanRequest request, CancellationToken cancellationToken)
    {
        Capture(request, cancellationToken);
        return Task.FromResult(new AiStudyPlanResponse(
            request.RequestId, request.CorrelationId, "برنامه", [], [], [], [], Metadata()));
    }

    public Task<AiHomeworkExplanationResponse> ExplainHomeworkAsync(AiHomeworkExplanationRequest request, CancellationToken cancellationToken)
    {
        Capture(request, cancellationToken);
        return Task.FromResult(new AiHomeworkExplanationResponse(
            request.RequestId, request.CorrelationId, "توضیح", [], [], [], [], Metadata()));
    }

    public Task<AiExamPreparationResponse> PrepareExamAsync(AiExamPreparationRequest request, CancellationToken cancellationToken)
    {
        Capture(request, cancellationToken);
        return Task.FromResult(new AiExamPreparationResponse(
            request.RequestId, request.CorrelationId, "آمادگی", [], [], [], [], [], Metadata()));
    }

    public Task<AiParentChildSummaryResponse> SummarizeChildAsync(AiParentChildSummaryRequest request, CancellationToken cancellationToken)
    {
        Capture(request, cancellationToken);
        return Task.FromResult(new AiParentChildSummaryResponse(
            request.RequestId,
            request.CorrelationId,
            "خلاصه تحصیلی",
            new AiProgress("Unknown", "داده کافی نیست"),
            [], [], [], [], [],
            Metadata()));
    }

    public Task<AiRoleInsightResponse> SummarizeTeacherClassAsync(AiTeacherClassSummaryRequest request, CancellationToken cancellationToken) =>
        RoleInsight(request, cancellationToken);

    public Task<AiRoleInsightResponse> GetTeacherStudentInsightAsync(AiTeacherStudentInsightRequest request, CancellationToken cancellationToken) =>
        RoleInsight(request, cancellationToken);

    public Task<AiActivitySuggestionsResponse> SuggestTeacherActivitiesAsync(AiTeacherActivitySuggestionsRequest request, CancellationToken cancellationToken)
    {
        Capture(request, cancellationToken);
        return Task.FromResult(new AiActivitySuggestionsResponse(
            request.RequestId, request.CorrelationId, "پیشنهادها", [], [], [], Metadata()));
    }

    public Task<AiRoleInsightResponse> SummarizeManagerSchoolAsync(AiManagerSchoolSummaryRequest request, CancellationToken cancellationToken) =>
        RoleInsight(request, cancellationToken);

    public Task<AiRoleInsightResponse> GetManagerClassInsightAsync(AiManagerClassInsightRequest request, CancellationToken cancellationToken) =>
        RoleInsight(request, cancellationToken);

    public Task<AiRoleInsightResponse> GetManagerStudentInsightAsync(AiManagerStudentInsightRequest request, CancellationToken cancellationToken) =>
        RoleInsight(request, cancellationToken);

    private Task<AiRoleInsightResponse> RoleInsight(IAiServiceRequest request, CancellationToken cancellationToken)
    {
        Capture(request, cancellationToken);
        return Task.FromResult(new AiRoleInsightResponse(
            request.RequestId, request.CorrelationId, "بینش", [], [], [], [], [], Metadata()));
    }

    private void Capture(IAiServiceRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        control.Capture(request);
    }

    private static AiResponseMetadata Metadata() => new("integration-test", 1);
}
