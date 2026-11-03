namespace MakanApp.Domain.Assessment;

public sealed class ExamGradeRevision : IAcademicScoped
{
    public const int MaximumFeedbackLength = 10_000;
    public const int MaximumPrivateNoteLength = 10_000;
    public const int MaximumCorrectionReasonLength = 1_000;

    private ExamGradeRevision()
    {
    }

    private ExamGradeRevision(
        Guid id,
        AcademicScope scope,
        Guid examAttemptId,
        int revisionNumber,
        decimal totalScore,
        Guid createdByMembershipId,
        DateTime createdAtUtc,
        Guid? supersedesExamGradeRevisionId,
        string? correctionReason,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? evaluatorPrivateNote)
    {
        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        ExamAttemptId = examAttemptId;
        RevisionNumber = revisionNumber;
        TotalScore = totalScore;
        Status = ExamGradeRevisionStatus.Draft;
        CreatedByMembershipId = Scope.Kind == AcademicClassKind.OrganizationClass ? createdByMembershipId : null;
        CreatedByPersonalMembershipId = Scope.Kind == AcademicClassKind.PersonalClass ? createdByMembershipId : null;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        SupersedesExamGradeRevisionId = supersedesExamGradeRevisionId;
        CorrectionReason = correctionReason;
        LearnerFeedback = learnerFeedback;
        GuardianVisibleFeedback = guardianVisibleFeedback;
        EvaluatorPrivateNote = evaluatorPrivateNote;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid ExamAttemptId { get; private set; }
    public int RevisionNumber { get; private set; }
    public decimal TotalScore { get; private set; }
    public ExamGradeRevisionStatus Status { get; private set; }
    public string? LearnerFeedback { get; private set; }
    public string? GuardianVisibleFeedback { get; private set; }
    public string? EvaluatorPrivateNote { get; private set; }
    public Guid? CreatedByMembershipId { get; private set; }
    public Guid? CreatedByPersonalMembershipId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? SupersedesExamGradeRevisionId { get; private set; }
    public string? CorrectionReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsMutable => Status is ExamGradeRevisionStatus.Draft or ExamGradeRevisionStatus.ReadyForRelease;
    public bool IsReleased => Status == ExamGradeRevisionStatus.Released;

    public static ExamGradeRevision CreateDraft(
        AcademicScope scope,
        Guid examAttemptId,
        Guid createdByMembershipId,
        DateTime createdAtUtc) =>
        Create(
            scope,
            examAttemptId,
            1,
            0,
            createdByMembershipId,
            createdAtUtc,
            null,
            null,
            null,
            null,
            null);

    public static ExamGradeRevision CreateCorrection(
        ExamGradeRevision releasedRevision,
        Guid createdByMembershipId,
        string correctionReason,
        DateTime createdAtUtc)
    {
        if (!releasedRevision.IsReleased)
        {
            throw new InvalidOperationException("فقط نمره منتشرشده می‌تواند مبنای اصلاح باشد.");
        }

        var normalizedReason = Normalize(correctionReason);
        if (normalizedReason is null || normalizedReason.Length > MaximumCorrectionReasonLength)
        {
            throw new ArgumentException(
                "دلیل اصلاح الزامی و دارای طول معتبر است.",
                nameof(correctionReason));
        }

        return Create(
            releasedRevision.Scope,
            releasedRevision.ExamAttemptId,
            checked(releasedRevision.RevisionNumber + 1),
            releasedRevision.TotalScore,
            createdByMembershipId,
            createdAtUtc,
            releasedRevision.Id,
            normalizedReason,
            releasedRevision.LearnerFeedback,
            releasedRevision.GuardianVisibleFeedback,
            releasedRevision.EvaluatorPrivateNote);
    }

    public void Recalculate(
        IReadOnlyCollection<ExamQuestionGrade> questionGrades,
        decimal examMaximumScore,
        DateTime updatedAtUtc)
    {
        EnsureMutable();
        var totalScore = ValidateQuestionGrades(questionGrades, examMaximumScore, requireReviewed: false);
        TotalScore = totalScore;
        Status = ExamGradeRevisionStatus.Draft;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void CompleteReview(
        IReadOnlyCollection<ExamQuestionGrade> questionGrades,
        decimal examMaximumScore,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? evaluatorPrivateNote,
        DateTime completedAtUtc)
    {
        EnsureMutable();
        var totalScore = ValidateQuestionGrades(questionGrades, examMaximumScore, requireReviewed: true);
        ValidateLength(learnerFeedback, MaximumFeedbackLength, nameof(learnerFeedback));
        ValidateLength(guardianVisibleFeedback, MaximumFeedbackLength, nameof(guardianVisibleFeedback));
        ValidateLength(evaluatorPrivateNote, MaximumPrivateNoteLength, nameof(evaluatorPrivateNote));
        TotalScore = totalScore;
        LearnerFeedback = Normalize(learnerFeedback);
        GuardianVisibleFeedback = Normalize(guardianVisibleFeedback);
        EvaluatorPrivateNote = Normalize(evaluatorPrivateNote);
        Status = ExamGradeRevisionStatus.ReadyForRelease;
        UpdatedAtUtc = EnsureUtc(completedAtUtc, nameof(completedAtUtc));
    }

    public void Release(
        IReadOnlyCollection<ExamQuestionGrade> questionGrades,
        decimal examMaximumScore,
        DateTime releasedAtUtc)
    {
        if (Status != ExamGradeRevisionStatus.ReadyForRelease)
        {
            throw new ExamGradeIncompleteException();
        }

        var totalScore = ValidateQuestionGrades(questionGrades, examMaximumScore, requireReviewed: true);
        if (totalScore != TotalScore)
        {
            throw new ExamGradeIncompleteException();
        }

        Status = ExamGradeRevisionStatus.Released;
        UpdatedAtUtc = EnsureUtc(releasedAtUtc, nameof(releasedAtUtc));
    }

    public void MarkSuperseded(DateTime supersededAtUtc)
    {
        if (!IsReleased)
        {
            throw new InvalidOperationException("فقط نمره منتشرشده می‌تواند تاریخی شود.");
        }

        Status = ExamGradeRevisionStatus.Superseded;
        UpdatedAtUtc = EnsureUtc(supersededAtUtc, nameof(supersededAtUtc));
    }

    private static ExamGradeRevision Create(
        AcademicScope scope,
        Guid examAttemptId,
        int revisionNumber,
        decimal totalScore,
        Guid createdByMembershipId,
        DateTime createdAtUtc,
        Guid? supersedesExamGradeRevisionId,
        string? correctionReason,
        string? learnerFeedback,
        string? guardianVisibleFeedback,
        string? evaluatorPrivateNote)
    {
        if (scope.Id == Guid.Empty ||
            examAttemptId == Guid.Empty ||
            createdByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های نمره آزمون الزامی هستند.");
        }

        if (revisionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }

        ValidateScore(totalScore, ExamVersion.MaximumScoreValue, nameof(totalScore));
        return new ExamGradeRevision(
            Guid.NewGuid(),
            scope,
            examAttemptId,
            revisionNumber,
            totalScore,
            createdByMembershipId,
            EnsureUtc(createdAtUtc, nameof(createdAtUtc)),
            supersedesExamGradeRevisionId,
            correctionReason,
            learnerFeedback,
            guardianVisibleFeedback,
            evaluatorPrivateNote);
    }

    private decimal ValidateQuestionGrades(
        IReadOnlyCollection<ExamQuestionGrade> questionGrades,
        decimal examMaximumScore,
        bool requireReviewed)
    {
        ValidateScore(examMaximumScore, ExamVersion.MaximumScoreValue, nameof(examMaximumScore));
        if (questionGrades.Count == 0 ||
            questionGrades.Any(grade =>
                grade.Scope != Scope ||
                grade.ExamAttemptId != ExamAttemptId ||
                grade.ExamGradeRevisionId != Id))
        {
            throw new InvalidOperationException("اقلام نمره متعلق به همین بازبینی آزمون نیستند.");
        }

        if (requireReviewed && questionGrades.Any(grade => !grade.IsReviewed))
        {
            throw new ExamGradeIncompleteException();
        }

        var maximumSum = questionGrades.Sum(grade => grade.MaximumScore);
        var totalScore = questionGrades.Sum(grade => grade.AwardedScore);
        if (maximumSum != examMaximumScore)
        {
            throw new ExamGradeIncompleteException();
        }

        ValidateScore(totalScore, examMaximumScore, nameof(totalScore));
        return totalScore;
    }

    private static void ValidateScore(decimal score, decimal maximumScore, string parameterName)
    {
        if (maximumScore <= 0 ||
            maximumScore > ExamVersion.MaximumScoreValue ||
            score < 0 ||
            score > maximumScore ||
            decimal.Round(score, 2) != score)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private void EnsureMutable()
    {
        if (!IsMutable)
        {
            throw new InvalidOperationException("نمره منتشرشده یا تاریخی قابل تغییر مستقیم نیست.");
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

public sealed class ExamGradeIncompleteException : Exception;
