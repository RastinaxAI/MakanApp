using MakanApp.Domain.Assessment;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Application.Assessment;

public interface IExamService
{
    Task<ExamEditorDto> CreateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CreateExamDraftCommand command,
        CancellationToken cancellationToken, AcademicClassKind classKind = AcademicClassKind.OrganizationClass);

    Task<ExamEditorDto> GetEditorAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken);

    Task<ExamTeacherPreview> GetTeacherPreviewAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken);

    Task<ExamEditorDto> UpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        UpdateExamDraftCommand command,
        CancellationToken cancellationToken);

    Task<ExamEditorDto> AddQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        AddExamQuestionCommand command,
        CancellationToken cancellationToken);

    Task<ExamEditorDto> UpdateQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        Guid questionId,
        UpdateExamQuestionCommand command,
        CancellationToken cancellationToken);

    Task DeleteQuestionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        Guid questionId,
        DeleteExamQuestionCommand command,
        CancellationToken cancellationToken);

    Task<ExamEditorDto> PublishAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        PublishExamCommand command,
        CancellationToken cancellationToken);

    Task<ExamEditorDto> CreateNextVersionAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CreateNextExamVersionCommand command,
        CancellationToken cancellationToken);

    Task<StudentSafeExamPreview> GetStudentPreviewAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentExamSummary>> GetMyExamsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken, Guid? personalClassId = null);
}

public interface IExamStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);
    Task<AcademicClassRecord?> GetClassForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken cancellationToken);
    Task<bool> HasActiveTeacherAssignmentAsync(AcademicScope scope, Guid classId, Guid membershipId, CancellationToken cancellationToken);
    Task<ExamAggregateRecord?> GetCurrentAsync(AcademicScope scope, Guid examId, CancellationToken cancellationToken);
    Task<ExamAggregateRecord?> GetCurrentForUpdateAsync(AcademicScope scope, Guid examId, CancellationToken cancellationToken);
    Task<ExamAggregateRecord?> GetLatestPublishedAsync(AcademicScope scope, Guid examId, CancellationToken cancellationToken);
    Task<bool> IsStudentActivelyEnrolledAsync(AcademicScope scope, Guid classId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetActiveLearnerUserIdsForUpdateAsync(AcademicScope scope, Guid classId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExamSummaryRecord>> GetForManagerAsync(AcademicScope scope, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExamSummaryRecord>> GetForTeacherAsync(AcademicScope scope, Guid membershipId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExamSummaryRecord>> GetForStudentAsync(AcademicScope scope, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ExamSummaryRecord>> GetForLearnerAsync(AcademicScope scope, Guid learnerOrganizationPersonId, CancellationToken cancellationToken);
    void SetOriginalRowVersion(Exam exam, byte[] rowVersion);
    void SetOriginalRowVersion(ExamVersion version, byte[] rowVersion);
    void SetOriginalRowVersion(QuestionVersion question, byte[] rowVersion);
    void Add(Exam exam);
    void Add(ExamVersion version);
    void Add(QuestionVersion question);
    void AddRange(IEnumerable<QuestionVersion> questions);
    void ReplaceOptions(
        QuestionVersion question,
        IEnumerable<QuestionOption> previousOptions);
    void Remove(QuestionVersion question);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
