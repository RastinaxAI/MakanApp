using System.Text.Json;
using System.Text.Json.Serialization;

namespace MakanApp.Application.Ai;

public sealed record CreateStudyPlanCommand(
    IReadOnlyCollection<string> Goals,
    int? AvailableMinutes,
    string? Notes);

public sealed record ExplainHomeworkCommand(string Question);

public sealed record SuggestTeacherActivitiesCommand(
    string Objective,
    int? AvailableMinutes,
    IReadOnlyCollection<string> Materials,
    string? Notes);

public sealed record AiStudentContext(
    AiActor Actor,
    AiScope Scope,
    AiAuthorizedContext Data);

public sealed record AiParentChildContext(
    AiActor Actor,
    AiScope Scope,
    AiAuthorizedContext Data);

public sealed record AiTeacherClassContext(
    AiActor Actor,
    AiScope Scope,
    AiTeacherClassData Data);

public sealed record AiTeacherStudentContext(
    AiActor Actor,
    AiScope Scope,
    AiTeacherStudentData Data);

public sealed record AiManagerSchoolContext(
    AiActor Actor,
    AiScope Scope,
    AiManagerSchoolData Data);

public sealed record AiManagerClassContext(
    AiActor Actor,
    AiScope Scope,
    AiManagerClassData Data);

public sealed record AiManagerStudentContext(
    AiActor Actor,
    AiScope Scope,
    AiManagerStudentData Data);

public sealed record AiSelectedHomework(
    Guid AssignmentId,
    Guid ClassId,
    string Title,
    string Description,
    DateTime DueAtUtc,
    decimal MaxScore);

public sealed record AiSelectedExam(
    Guid ExamId,
    Guid ClassId,
    string Title,
    string? Description,
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    decimal MaxScore);

public sealed record AiLearnerClassScope(
    Guid ClassId,
    string ClassTitle,
    string CourseTitle);

public sealed record AiStudyPreferences(
    int? AvailableMinutes,
    string? Notes);

public sealed record AiActivityConstraints(
    int? AvailableMinutes,
    IReadOnlyCollection<string> Materials,
    string? Notes);

public sealed record AiTeacherClassScope(
    string OrganizationId,
    string SchoolId,
    string ClassId);

public sealed record AiTeacherStudentScope(
    string OrganizationId,
    string SchoolId,
    string ClassId,
    string StudentId);

public sealed record AiManagerSchoolScope(
    string OrganizationId,
    string SchoolId);

public sealed record AiManagerClassScope(
    string OrganizationId,
    string SchoolId,
    string ClassId);

public sealed record AiManagerStudentScope(
    string OrganizationId,
    string SchoolId,
    string ClassId,
    string StudentId);

public sealed record AiTeacherAggregateMetrics(
    double? AverageScore,
    string? ScoreScale,
    double? CompletionRate,
    double? AttendanceRate,
    string? Trend);

public sealed record AiAggregateMetrics(
    double? AverageScore,
    string? ScoreScale,
    double? CompletionRate,
    double? AttendanceRate,
    string? Trend,
    int? StudentCount = null,
    int? ClassCount = null);

public sealed record AiTeacherClassData(
    string ClassId,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Subjects,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Grades,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Homeworks,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Exams,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Activity,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Students,
    AiTeacherAggregateMetrics? Aggregates);

public sealed record AiTeacherStudentData(
    string StudentId,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Subjects,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Grades,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Homeworks,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Exams,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Activity);

public sealed record AiManagerSchoolData(
    string SchoolId,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Subjects,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Grades,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Homeworks,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Exams,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Activity,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Classes,
    AiAggregateMetrics? Aggregates);

public sealed record AiManagerClassData(
    string ClassId,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Subjects,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Grades,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Homeworks,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Exams,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Activity,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Students,
    AiAggregateMetrics? Aggregates);

public sealed record AiManagerStudentData(
    string StudentId,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Subjects,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Grades,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Homeworks,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Exams,
    IReadOnlyCollection<IReadOnlyDictionary<string, JsonElement>> Activity);

public sealed record AiStudentAnalysisRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiScope Scope,
    AiAuthorizedContext StudentData,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiStudyPlanRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiScope Scope,
    AiAuthorizedContext StudentData,
    IReadOnlyCollection<string> Goals,
    AiStudyPreferences Preferences,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiHomeworkExplanationRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiScope Scope,
    IReadOnlyDictionary<string, JsonElement> Homework,
    string Question,
    AiAuthorizedContext Context,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiExamPreparationRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiScope Scope,
    IReadOnlyDictionary<string, JsonElement> Exam,
    AiAuthorizedContext StudentData,
    AiAuthorizedContext Context,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiParentChildSummaryRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiScope Scope,
    AiAuthorizedContext ChildData,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiTeacherClassSummaryRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiTeacherClassScope Scope,
    AiTeacherClassData ClassData,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiTeacherStudentInsightRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiTeacherStudentScope Scope,
    AiTeacherStudentData StudentData,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiTeacherActivitySuggestionsRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiTeacherClassScope Scope,
    AiTeacherClassData ClassData,
    string Objective,
    AiActivityConstraints Constraints,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiManagerSchoolSummaryRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiManagerSchoolScope Scope,
    AiManagerSchoolData SchoolData,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiManagerClassInsightRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiManagerClassScope Scope,
    AiManagerClassData ClassData,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiManagerStudentInsightRequest(
    string RequestId,
    string? CorrelationId,
    AiActor Actor,
    AiManagerStudentScope Scope,
    AiManagerStudentData StudentData,
    string Language = "fa") : IAiServiceRequest;

public sealed record AiStudentAnalysisResponse(
    string RequestId,
    string? CorrelationId,
    string Summary,
    IReadOnlyCollection<AiInsight> Strengths,
    IReadOnlyCollection<AiInsight> Weaknesses,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;

public sealed record AiPlanItem(
    string Title,
    string Reason,
    [property: JsonRequired] AiSeverity Priority,
    int? EstimatedMinutes);

public sealed record AiStudyPlanResponse(
    string RequestId,
    string? CorrelationId,
    string Summary,
    IReadOnlyCollection<string> Priorities,
    IReadOnlyCollection<AiPlanItem> PlanItems,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;

public sealed record AiHomeworkExplanationResponse(
    string RequestId,
    string? CorrelationId,
    string Explanation,
    IReadOnlyCollection<string> Steps,
    IReadOnlyCollection<string> Hints,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;

public sealed record AiExamPreparationResponse(
    string RequestId,
    string? CorrelationId,
    string Summary,
    IReadOnlyCollection<string> PriorityTopics,
    IReadOnlyCollection<string> PreparationSteps,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;

public sealed record AiProgress(string Trend, string Description);

public sealed record AiAttentionArea(
    string Title,
    string Description,
    AiSeverity Severity,
    string? EntityType,
    string? EntityId);

public sealed record AiUpcomingItem(
    string Type,
    string Id,
    string Title,
    string? Date);

public sealed record AiParentChildSummaryResponse(
    string RequestId,
    string? CorrelationId,
    string AcademicSummary,
    AiProgress Progress,
    IReadOnlyCollection<AiAttentionArea> AttentionAreas,
    IReadOnlyCollection<AiUpcomingItem> UpcomingItems,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;

public sealed record AiRoleInsightResponse(
    string RequestId,
    string? CorrelationId,
    string Summary,
    IReadOnlyCollection<AiInsight> Strengths,
    IReadOnlyCollection<AiInsight> AttentionAreas,
    IReadOnlyCollection<AiInsight> Risks,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;

public sealed record AiTeachingActivity(
    string Title,
    string Objective,
    string Reason,
    IReadOnlyCollection<string> Steps,
    int? EstimatedMinutes);

public sealed record AiActivitySuggestionsResponse(
    string RequestId,
    string? CorrelationId,
    string Summary,
    IReadOnlyCollection<AiTeachingActivity> Activities,
    IReadOnlyCollection<AiRecommendation> Recommendations,
    IReadOnlyCollection<AiSourceReference> Sources,
    AiResponseMetadata Metadata) : IAiServiceResponse;
