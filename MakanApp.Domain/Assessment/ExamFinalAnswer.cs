namespace MakanApp.Domain.Assessment;

public sealed class ExamFinalAnswer : IAcademicScoped
{
    private ExamFinalAnswer()
    {
    }

    private ExamFinalAnswer(
        AcademicScope scope,
        Guid examAttemptId,
        Guid examAttemptQuestionId,
        Guid questionVersionId,
        Guid answerRevisionId)
    {
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        ExamAttemptId = examAttemptId;
        ExamAttemptQuestionId = examAttemptQuestionId;
        QuestionVersionId = questionVersionId;
        AnswerRevisionId = answerRevisionId;
    }

    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamAttemptQuestionId { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public Guid AnswerRevisionId { get; private set; }

    public static ExamFinalAnswer Create(
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        AnswerRevision revision)
    {
        if (!attempt.IsInProgress ||
            attemptQuestion.Scope != attempt.Scope ||
            attemptQuestion.ExamAttemptId != attempt.Id ||
            revision.Scope != attempt.Scope ||
            revision.ExamAttemptId != attempt.Id ||
            revision.ExamAttemptQuestionId != attemptQuestion.Id ||
            revision.QuestionVersionId != attemptQuestion.QuestionVersionId ||
            attemptQuestion.CurrentAnswerRevisionId != revision.Id)
        {
            throw new InvalidOperationException("پاسخ نهایی باید همان پاسخ پذیرفته‌شده جاری سؤال تلاش باشد.");
        }

        return new ExamFinalAnswer(
            attempt.Scope,
            attempt.Id,
            attemptQuestion.Id,
            attemptQuestion.QuestionVersionId,
            revision.Id);
    }
}
