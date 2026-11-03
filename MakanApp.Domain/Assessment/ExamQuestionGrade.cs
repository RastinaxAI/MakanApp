namespace MakanApp.Domain.Assessment;

public sealed class ExamQuestionGrade : IAcademicScoped
{
    public const int MaximumFeedbackLength = 10_000;
    public const int MaximumPrivateNoteLength = 10_000;

    private ExamQuestionGrade()
    {
    }

    private ExamQuestionGrade(
        Guid id,
        AcademicScope scope,
        Guid examAttemptId,
        Guid examGradeRevisionId,
        Guid examAttemptQuestionId,
        Guid questionVersionId,
        ExamQuestionGradingMode gradingMode,
        bool isAnswered,
        decimal maximumScore,
        decimal awardedScore,
        bool isReviewed,
        string? learnerFeedback,
        string? evaluatorPrivateNote,
        Guid? reviewedByMembershipId,
        DateTime? reviewedAtUtc)
    {
        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        ExamAttemptId = examAttemptId;
        ExamGradeRevisionId = examGradeRevisionId;
        ExamAttemptQuestionId = examAttemptQuestionId;
        QuestionVersionId = questionVersionId;
        GradingMode = gradingMode;
        IsAnswered = isAnswered;
        MaximumScore = maximumScore;
        AwardedScore = awardedScore;
        IsReviewed = isReviewed;
        LearnerFeedback = learnerFeedback;
        EvaluatorPrivateNote = evaluatorPrivateNote;
        ReviewedByMembershipId = Scope.Kind == AcademicClassKind.OrganizationClass ? reviewedByMembershipId : null;
        ReviewedByPersonalMembershipId = Scope.Kind == AcademicClassKind.PersonalClass ? reviewedByMembershipId : null;
        ReviewedAtUtc = reviewedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamGradeRevisionId { get; private set; }
    public Guid ExamAttemptQuestionId { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public ExamQuestionGradingMode GradingMode { get; private set; }
    public bool IsAnswered { get; private set; }
    public decimal MaximumScore { get; private set; }
    public decimal AwardedScore { get; private set; }
    public bool IsReviewed { get; private set; }
    public string? LearnerFeedback { get; private set; }
    public string? EvaluatorPrivateNote { get; private set; }
    public Guid? ReviewedByMembershipId { get; private set; }
    public Guid? ReviewedByPersonalMembershipId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }

    public static ExamQuestionGrade Create(
        ExamGradeRevision gradeRevision,
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        QuestionVersion question,
        ExamFinalAnswer? finalAnswer,
        AnswerRevision? finalRevision,
        Guid evaluatorMembershipId,
        DateTime createdAtUtc)
    {
        ValidateSource(gradeRevision, attempt, attemptQuestion, question, finalAnswer, finalRevision);
        if (evaluatorMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه ارزیاب الزامی است.", nameof(evaluatorMembershipId));
        }

        createdAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        var isAnswered = finalRevision?.HasAnswer == true;
        if (question.Type == ExamQuestionType.ObjectiveSingleChoice)
        {
            var correctOptionId = question.Options.Single(option => option.IsCorrect).Id;
            var awardedScore = finalRevision?.SelectedOptionId == correctOptionId
                ? question.Score
                : 0;
            return new ExamQuestionGrade(
                Guid.NewGuid(),
                attempt.Scope,
                attempt.Id,
                gradeRevision.Id,
                attemptQuestion.Id,
                question.Id,
                ExamQuestionGradingMode.Objective,
                isAnswered,
                question.Score,
                awardedScore,
                true,
                null,
                null,
                evaluatorMembershipId,
                createdAtUtc);
        }

        return new ExamQuestionGrade(
            Guid.NewGuid(),
            attempt.Scope,
            attempt.Id,
            gradeRevision.Id,
            attemptQuestion.Id,
            question.Id,
            ExamQuestionGradingMode.Manual,
            isAnswered,
            question.Score,
            0,
            false,
            null,
            null,
            null,
            null);
    }

    public ExamQuestionGrade CopyTo(ExamGradeRevision correction)
    {
        if (correction.Scope != Scope ||
            correction.ExamAttemptId != ExamAttemptId ||
            correction.SupersedesExamGradeRevisionId != ExamGradeRevisionId)
        {
            throw new InvalidOperationException("نسخه اصلاحی با نمره سؤال سازگار نیست.");
        }

        return new ExamQuestionGrade(
            Guid.NewGuid(),
            Scope,
            ExamAttemptId,
            correction.Id,
            ExamAttemptQuestionId,
            QuestionVersionId,
            GradingMode,
            IsAnswered,
            MaximumScore,
            AwardedScore,
            IsReviewed,
            LearnerFeedback,
            EvaluatorPrivateNote,
            ReviewedByMembershipId ?? ReviewedByPersonalMembershipId,
            ReviewedAtUtc);
    }

    public void ReviewManually(
        decimal awardedScore,
        string? learnerFeedback,
        string? evaluatorPrivateNote,
        Guid reviewedByMembershipId,
        DateTime reviewedAtUtc)
    {
        if (GradingMode != ExamQuestionGradingMode.Manual)
        {
            throw new InvalidOperationException("نمره سؤال objective از answer key محاسبه می‌شود.");
        }

        if (reviewedByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه ارزیاب الزامی است.", nameof(reviewedByMembershipId));
        }

        ValidateAwardedScore(awardedScore, MaximumScore);
        ValidateLength(learnerFeedback, MaximumFeedbackLength, nameof(learnerFeedback));
        ValidateLength(evaluatorPrivateNote, MaximumPrivateNoteLength, nameof(evaluatorPrivateNote));
        AwardedScore = awardedScore;
        LearnerFeedback = Normalize(learnerFeedback);
        EvaluatorPrivateNote = Normalize(evaluatorPrivateNote);
        ReviewedByMembershipId = Scope.Kind == AcademicClassKind.OrganizationClass ? reviewedByMembershipId : null;
        ReviewedByPersonalMembershipId = Scope.Kind == AcademicClassKind.PersonalClass ? reviewedByMembershipId : null;
        ReviewedAtUtc = EnsureUtc(reviewedAtUtc, nameof(reviewedAtUtc));
        IsReviewed = true;
    }

    public static void ValidateAwardedScore(decimal awardedScore, decimal maximumScore)
    {
        if (maximumScore <= 0 ||
            maximumScore > ExamVersion.MaximumScoreValue ||
            awardedScore < 0 ||
            awardedScore > maximumScore ||
            decimal.Round(awardedScore, 2) != awardedScore)
        {
            throw new ArgumentOutOfRangeException(nameof(awardedScore));
        }
    }

    private static void ValidateSource(
        ExamGradeRevision gradeRevision,
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        QuestionVersion question,
        ExamFinalAnswer? finalAnswer,
        AnswerRevision? finalRevision)
    {
        if (attempt.Status != ExamAttemptStatus.Finalized ||
            gradeRevision.Scope != attempt.Scope ||
            gradeRevision.ExamAttemptId != attempt.Id ||
            attemptQuestion.Scope != attempt.Scope ||
            attemptQuestion.ExamAttemptId != attempt.Id ||
            question.Scope != attempt.Scope ||
            attemptQuestion.QuestionVersionId != question.Id ||
            question.ExamVersionId != attempt.ExamVersionId ||
            (finalAnswer is null) != (finalRevision is null) ||
            (finalAnswer is not null &&
             (finalAnswer.Scope != attempt.Scope ||
              finalAnswer.ExamAttemptId != attempt.Id ||
              finalAnswer.ExamAttemptQuestionId != attemptQuestion.Id ||
              finalAnswer.QuestionVersionId != question.Id ||
              finalAnswer.AnswerRevisionId != finalRevision!.Id)) ||
            (finalRevision is not null &&
             (finalRevision.Scope != attempt.Scope ||
              finalRevision.ExamAttemptId != attempt.Id ||
              finalRevision.ExamAttemptQuestionId != attemptQuestion.Id ||
              finalRevision.QuestionVersionId != question.Id)))
        {
            throw new InvalidOperationException("منبع نمره سؤال با snapshot نهایی آزمون سازگار نیست.");
        }
    }

    private static void ValidateLength(string? value, int maximumLength, string parameterName)
    {
        if (value?.Trim().Length > maximumLength)
        {
            throw new ArgumentException("طول متن از حد مجاز بیشتر است.", parameterName);
        }
    }

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new ArgumentException("زمان باید UTC باشد.", parameterName)
        };
}
