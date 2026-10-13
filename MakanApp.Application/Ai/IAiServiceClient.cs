namespace MakanApp.Application.Ai;

public interface IAiServiceClient
{
    Task<AiChatResponse> ChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken);

    Task<AiStudentAnalysisResponse> AnalyzeStudentAsync(AiStudentAnalysisRequest request, CancellationToken cancellationToken);
    Task<AiStudyPlanResponse> CreateStudyPlanAsync(AiStudyPlanRequest request, CancellationToken cancellationToken);
    Task<AiHomeworkExplanationResponse> ExplainHomeworkAsync(AiHomeworkExplanationRequest request, CancellationToken cancellationToken);
    Task<AiExamPreparationResponse> PrepareExamAsync(AiExamPreparationRequest request, CancellationToken cancellationToken);
    Task<AiParentChildSummaryResponse> SummarizeChildAsync(AiParentChildSummaryRequest request, CancellationToken cancellationToken);
    Task<AiRoleInsightResponse> SummarizeTeacherClassAsync(AiTeacherClassSummaryRequest request, CancellationToken cancellationToken);
    Task<AiRoleInsightResponse> GetTeacherStudentInsightAsync(AiTeacherStudentInsightRequest request, CancellationToken cancellationToken);
    Task<AiActivitySuggestionsResponse> SuggestTeacherActivitiesAsync(AiTeacherActivitySuggestionsRequest request, CancellationToken cancellationToken);
    Task<AiRoleInsightResponse> SummarizeManagerSchoolAsync(AiManagerSchoolSummaryRequest request, CancellationToken cancellationToken);
    Task<AiRoleInsightResponse> GetManagerClassInsightAsync(AiManagerClassInsightRequest request, CancellationToken cancellationToken);
    Task<AiRoleInsightResponse> GetManagerStudentInsightAsync(AiManagerStudentInsightRequest request, CancellationToken cancellationToken);
}
