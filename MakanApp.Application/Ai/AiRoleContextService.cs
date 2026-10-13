namespace MakanApp.Application.Ai;

public sealed class AiRoleContextService(
    IStudentContextBuilder studentContextBuilder,
    IParentChildContextBuilder parentChildContextBuilder,
    ITeacherClassContextBuilder teacherClassContextBuilder,
    ITeacherStudentContextBuilder teacherStudentContextBuilder,
    IManagerSchoolContextBuilder managerSchoolContextBuilder,
    IManagerClassContextBuilder managerClassContextBuilder,
    IManagerStudentContextBuilder managerStudentContextBuilder,
    IAiServiceClient client) : IAiRoleContextService
{
    public async Task<AiStudentAnalysisResponse> AnalyzeStudentAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var context = await studentContextBuilder.BuildAsync(userId, sessionId, cancellationToken);
        return await client.AnalyzeStudentAsync(
            new AiStudentAnalysisRequest(
                RequestId(), correlationId, context.Actor, context.Scope, context.Data),
            cancellationToken);
    }

    public async Task<AiStudyPlanResponse> CreateStudyPlanAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        CreateStudyPlanCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var goals = NormalizeValues(command.Goals, AiContextLimits.GoalCount, 200);
        var notes = NormalizeOptional(command.Notes, 1_000);
        if (command.AvailableMinutes is <= 0 or > 1_440)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        var context = await studentContextBuilder.BuildAsync(userId, sessionId, cancellationToken);
        return await client.CreateStudyPlanAsync(
            new AiStudyPlanRequest(
                RequestId(),
                correlationId,
                context.Actor,
                context.Scope,
                context.Data,
                goals,
                new AiStudyPreferences(command.AvailableMinutes, notes)),
            cancellationToken);
    }

    public async Task<AiHomeworkExplanationResponse> ExplainHomeworkAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        Guid assignmentId,
        ExplainHomeworkCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var question = NormalizeRequired(command.Question, AiContextLimits.FreeTextLength);
        var (context, homework) = await studentContextBuilder.BuildHomeworkAsync(
            userId, sessionId, assignmentId, cancellationToken);
        return await client.ExplainHomeworkAsync(
            new AiHomeworkExplanationRequest(
                RequestId(),
                correlationId,
                context.Actor,
                context.Scope,
                AiContextProjection.Homework(homework),
                question,
                context.Data),
            cancellationToken);
    }

    public async Task<AiExamPreparationResponse> PrepareExamAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        Guid examId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var (context, exam) = await studentContextBuilder.BuildExamAsync(
            userId, sessionId, examId, cancellationToken);
        return await client.PrepareExamAsync(
            new AiExamPreparationRequest(
                RequestId(),
                correlationId,
                context.Actor,
                context.Scope,
                AiContextProjection.Exam(exam),
                context.Data,
                context.Data),
            cancellationToken);
    }

    public async Task<AiParentChildSummaryResponse> SummarizeChildAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var context = await parentChildContextBuilder.BuildAsync(userId, sessionId, cancellationToken);
        return await client.SummarizeChildAsync(
            new AiParentChildSummaryRequest(
                RequestId(), correlationId, context.Actor, context.Scope, context.Data),
            cancellationToken);
    }

    public async Task<AiRoleInsightResponse> SummarizeTeacherClassAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var context = await teacherClassContextBuilder.BuildAsync(
            userId, sessionId, classId, cancellationToken);
        return await client.SummarizeTeacherClassAsync(
            new AiTeacherClassSummaryRequest(
                RequestId(), correlationId, context.Actor, TeacherClassScope(context.Scope), context.Data),
            cancellationToken);
    }

    public async Task<AiRoleInsightResponse> GetTeacherStudentInsightAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var context = await teacherStudentContextBuilder.BuildAsync(
            userId, sessionId, classId, studentId, cancellationToken);
        return await client.GetTeacherStudentInsightAsync(
            new AiTeacherStudentInsightRequest(
                RequestId(), correlationId, context.Actor, TeacherStudentScope(context.Scope), context.Data),
            cancellationToken);
    }

    public async Task<AiActivitySuggestionsResponse> SuggestTeacherActivitiesAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        Guid classId,
        SuggestTeacherActivitiesCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var objective = NormalizeRequired(command.Objective, 500);
        var materials = NormalizeValues(command.Materials, AiContextLimits.MaterialCount, 200);
        var notes = NormalizeOptional(command.Notes, 1_000);
        if (command.AvailableMinutes is <= 0 or > 1_440)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        var context = await teacherClassContextBuilder.BuildAsync(
            userId, sessionId, classId, cancellationToken);
        return await client.SuggestTeacherActivitiesAsync(
            new AiTeacherActivitySuggestionsRequest(
                RequestId(),
                correlationId,
                context.Actor,
                TeacherClassScope(context.Scope),
                context.Data,
                objective,
                new AiActivityConstraints(command.AvailableMinutes, materials, notes)),
            cancellationToken);
    }

    public async Task<AiRoleInsightResponse> SummarizeManagerSchoolAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var context = await managerSchoolContextBuilder.BuildAsync(userId, sessionId, cancellationToken);
        return await client.SummarizeManagerSchoolAsync(
            new AiManagerSchoolSummaryRequest(
                RequestId(), correlationId, context.Actor, ManagerSchoolScope(context.Scope), context.Data),
            cancellationToken);
    }

    public async Task<AiRoleInsightResponse> GetManagerClassInsightAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var context = await managerClassContextBuilder.BuildAsync(
            userId, sessionId, classId, cancellationToken);
        return await client.GetManagerClassInsightAsync(
            new AiManagerClassInsightRequest(
                RequestId(), correlationId, context.Actor, ManagerClassScope(context.Scope), context.Data),
            cancellationToken);
    }

    public async Task<AiRoleInsightResponse> GetManagerStudentInsightAsync(
        Guid userId,
        Guid sessionId,
        string correlationId,
        Guid classId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        ValidateCorrelation(correlationId);
        var context = await managerStudentContextBuilder.BuildAsync(
            userId, sessionId, classId, studentId, cancellationToken);
        return await client.GetManagerStudentInsightAsync(
            new AiManagerStudentInsightRequest(
                RequestId(), correlationId, context.Actor, ManagerStudentScope(context.Scope), context.Data),
            cancellationToken);
    }

    private static string RequestId() => Guid.NewGuid().ToString("N");

    private static AiTeacherClassScope TeacherClassScope(AiScope scope) => new(
        scope.OrganizationId!, scope.SchoolId!, scope.ClassId!);

    private static AiTeacherStudentScope TeacherStudentScope(AiScope scope) => new(
        scope.OrganizationId!, scope.SchoolId!, scope.ClassId!, scope.StudentId!);

    private static AiManagerSchoolScope ManagerSchoolScope(AiScope scope) => new(
        scope.OrganizationId!, scope.SchoolId!);

    private static AiManagerClassScope ManagerClassScope(AiScope scope) => new(
        scope.OrganizationId!, scope.SchoolId!, scope.ClassId!);

    private static AiManagerStudentScope ManagerStudentScope(AiScope scope) => new(
        scope.OrganizationId!, scope.SchoolId!, scope.ClassId!, scope.StudentId!);

    private static void ValidateCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
        {
            throw AiContextAuthorization.InvalidRequest();
        }
    }

    private static string NormalizeRequired(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value, int maximumLength)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        return normalized;
    }

    private static IReadOnlyCollection<string> NormalizeValues(
        IReadOnlyCollection<string>? values,
        int maximumCount,
        int maximumLength)
    {
        if (values is null || values.Count > maximumCount ||
            values.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > maximumLength))
        {
            throw AiContextAuthorization.InvalidRequest();
        }

        return values.Select(value => value.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }
}
