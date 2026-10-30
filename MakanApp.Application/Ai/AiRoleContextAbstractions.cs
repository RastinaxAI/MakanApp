namespace MakanApp.Application.Ai;

public interface IStudentContextBuilder
{
    Task<AiStudentContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<(AiStudentContext Context, AiSelectedHomework Homework)> BuildHomeworkAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<(AiStudentContext Context, AiSelectedExam Exam)> BuildExamAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken);
}

public interface IParentChildContextBuilder
{
    Task<AiParentChildContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface ITeacherClassContextBuilder
{
    Task<AiTeacherClassContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken);
}

public interface ITeacherStudentContextBuilder
{
    Task<AiTeacherStudentContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken);
}

public interface IManagerSchoolContextBuilder
{
    Task<AiManagerSchoolContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface IManagerClassContextBuilder
{
    Task<AiManagerClassContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken);
}

public interface IManagerStudentContextBuilder
{
    Task<AiManagerStudentContext> BuildAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken);
}

public interface IAiRoleContextService
{
    Task<AiStudentAnalysisResponse> AnalyzeStudentAsync(Guid userId, Guid sessionId, string correlationId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiStudyPlanResponse> CreateStudyPlanAsync(Guid userId, Guid sessionId, string correlationId, CreateStudyPlanCommand command, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiHomeworkExplanationResponse> ExplainHomeworkAsync(Guid userId, Guid sessionId, string correlationId, Guid assignmentId, ExplainHomeworkCommand command, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiExamPreparationResponse> PrepareExamAsync(Guid userId, Guid sessionId, string correlationId, Guid examId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiParentChildSummaryResponse> SummarizeChildAsync(Guid userId, Guid sessionId, string correlationId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiRoleInsightResponse> SummarizeTeacherClassAsync(Guid userId, Guid sessionId, string correlationId, Guid classId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiRoleInsightResponse> GetTeacherStudentInsightAsync(Guid userId, Guid sessionId, string correlationId, Guid classId, Guid studentId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiActivitySuggestionsResponse> SuggestTeacherActivitiesAsync(Guid userId, Guid sessionId, string correlationId, Guid classId, SuggestTeacherActivitiesCommand command, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiRoleInsightResponse> SummarizeManagerSchoolAsync(Guid userId, Guid sessionId, string correlationId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiRoleInsightResponse> GetManagerClassInsightAsync(Guid userId, Guid sessionId, string correlationId, Guid classId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
    Task<AiRoleInsightResponse> GetManagerStudentInsightAsync(Guid userId, Guid sessionId, string correlationId, Guid classId, Guid studentId, CancellationToken cancellationToken, AiInvocationOptions? invocation = null);
}
