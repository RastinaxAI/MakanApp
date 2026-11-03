namespace MakanApp.Domain.Assessment;

public sealed class SubmissionAttempt : IAcademicScoped
{
    public const int MaximumAnswerLength = 50_000;

    private SubmissionAttempt()
    {
    }

    private SubmissionAttempt(
        Guid id,
        AcademicScope scope,
        Guid assignmentId,
        Guid assignmentVersionId,
        Guid assignmentRecipientId,
        AcademicParticipantRef participation,
        int attemptNumber,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty || scope.Id == Guid.Empty || assignmentId == Guid.Empty ||
            assignmentVersionId == Guid.Empty || assignmentRecipientId == Guid.Empty ||
            participation.Id == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های تلاش ارسال الزامی هستند.");
        }

        if (attemptNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان ایجاد تلاش باید UTC باشد.", nameof(createdAtUtc));
        }

        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        AssignmentId = assignmentId;
        AssignmentVersionId = assignmentVersionId;
        AssignmentRecipientId = assignmentRecipientId;
        participation.EnsureScope(scope);
        EnrollmentId = participation.EnrollmentId;
        PersonalClassMembershipId = participation.PersonalClassMembershipId;
        ParticipantId = participation.Id;
        AttemptNumber = attemptNumber;
        Status = SubmissionAttemptStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid AssignmentId { get; private set; }
    public Guid AssignmentVersionId { get; private set; }
    public Guid AssignmentRecipientId { get; private set; }
    public Guid? EnrollmentId { get; private set; }
    public Guid? PersonalClassMembershipId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public AcademicParticipantRef Participation => new(ContextKind, ParticipantId);
    public int AttemptNumber { get; private set; }
    public SubmissionAttemptStatus Status { get; private set; }
    public string? AnswerText { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastSavedAtUtc { get; private set; }
    public DateTime? SubmittedAtUtc { get; private set; }
    public bool IsLate { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Status == SubmissionAttemptStatus.Draft;

    public static SubmissionAttempt CreateDraft(
        AcademicScope scope,
        Guid assignmentId,
        Guid assignmentVersionId,
        Guid assignmentRecipientId,
        AcademicParticipantRef participation,
        int attemptNumber,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            scope,
            assignmentId,
            assignmentVersionId,
            assignmentRecipientId,
            participation,
            attemptNumber,
            createdAtUtc);

    public void SaveAnswer(string? answerText, DateTime savedAtUtc)
    {
        EnsureDraft();
        AnswerText = NormalizeAnswer(answerText);
        LastSavedAtUtc = EnsureUtc(savedAtUtc, nameof(savedAtUtc));
    }

    public void MarkAttachmentsChanged(DateTime changedAtUtc)
    {
        EnsureDraft();
        LastSavedAtUtc = EnsureUtc(changedAtUtc, nameof(changedAtUtc));
    }

    public void Submit(
        DateTime submittedAtUtc,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        bool hasReadyAttachment)
    {
        EnsureDraft();
        submittedAtUtc = EnsureUtc(submittedAtUtc, nameof(submittedAtUtc));
        dueAtUtc = EnsureUtc(dueAtUtc, nameof(dueAtUtc));

        var isLate = submittedAtUtc > dueAtUtc;
        if (isLate && !allowLateSubmission)
        {
            throw new SubmissionDeadlinePassedException();
        }

        if (string.IsNullOrWhiteSpace(AnswerText) && !hasReadyAttachment)
        {
            throw new SubmissionEmptyException();
        }

        Status = SubmissionAttemptStatus.Submitted;
        SubmittedAtUtc = submittedAtUtc;
        IsLate = isLate;
        LastSavedAtUtc ??= submittedAtUtc;
    }

    private static string? NormalizeAnswer(string? answerText)
    {
        var normalized = answerText?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > MaximumAnswerLength)
        {
            throw new ArgumentException("متن پاسخ از طول مجاز بیشتر است.", nameof(answerText));
        }

        return normalized;
    }

    private void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("تلاش ارسال‌شده تغییرپذیر نیست.");
        }
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            // SQL Server datetime2 اطلاعات Kind را نگه نمی‌دارد؛ مقدار persist‌شده همچنان UTC است.
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new ArgumentException("زمان باید UTC باشد.", parameterName)
        };
}

public sealed class SubmissionDeadlinePassedException : Exception
{
}

public sealed class SubmissionEmptyException : Exception
{
}
