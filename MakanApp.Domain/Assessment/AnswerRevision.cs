namespace MakanApp.Domain.Assessment;

public sealed class AnswerRevision
{
    private readonly List<AnswerRevisionAttachment> _attachments = [];
    public const int MaximumTextAnswerLength = 20_000;
    public const int RequestHashLength = 64;

    private AnswerRevision()
    {
    }

    private AnswerRevision(
        Guid id,
        Guid organizationId,
        Guid examAttemptId,
        Guid examAttemptQuestionId,
        Guid questionVersionId,
        int revisionNumber,
        ExamQuestionType answerType,
        Guid? selectedOptionId,
        string? textAnswer,
        Guid clientOperationId,
        string requestHash,
        Guid createdBySessionId,
        Guid? supersedesAnswerRevisionId,
        long acceptedWriteLeaseVersion,
        long acceptedAnswerSetVersion,
        DateTime acceptedAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ExamAttemptId = examAttemptId;
        ExamAttemptQuestionId = examAttemptQuestionId;
        QuestionVersionId = questionVersionId;
        RevisionNumber = revisionNumber;
        AnswerType = answerType;
        SelectedOptionId = selectedOptionId;
        TextAnswer = textAnswer;
        ClientOperationId = clientOperationId;
        RequestHash = requestHash;
        CreatedBySessionId = createdBySessionId;
        SupersedesAnswerRevisionId = supersedesAnswerRevisionId;
        AcceptedWriteLeaseVersion = acceptedWriteLeaseVersion;
        AcceptedAnswerSetVersion = acceptedAnswerSetVersion;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamAttemptQuestionId { get; private set; }
    public Guid QuestionVersionId { get; private set; }
    public int RevisionNumber { get; private set; }
    public ExamQuestionType AnswerType { get; private set; }
    public Guid? SelectedOptionId { get; private set; }
    public string? TextAnswer { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public Guid CreatedBySessionId { get; private set; }
    public Guid? SupersedesAnswerRevisionId { get; private set; }
    public long AcceptedWriteLeaseVersion { get; private set; }
    public long AcceptedAnswerSetVersion { get; private set; }
    public DateTime AcceptedAtUtc { get; private set; }
    public IReadOnlyCollection<AnswerRevisionAttachment> Attachments => _attachments;
    public bool HasAnswer => SelectedOptionId.HasValue || !string.IsNullOrEmpty(TextAnswer) ||
        AnswerType == ExamQuestionType.Descriptive && _attachments.Count > 0;

    public static AnswerRevision CreateObjective(
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        QuestionVersion question,
        Guid selectedOptionId,
        int revisionNumber,
        Guid clientOperationId,
        string requestHash,
        Guid createdBySessionId,
        Guid? supersedesAnswerRevisionId,
        long acceptedWriteLeaseVersion,
        long acceptedAnswerSetVersion,
        DateTime acceptedAtUtc,
        IReadOnlyCollection<Guid>? attachmentFileIds = null)
    {
        EnsureRelationship(attempt, attemptQuestion, question);
        if (question.Type != ExamQuestionType.ObjectiveSingleChoice)
        {
            throw new ExamAnswerTypeInvalidException();
        }

        if (selectedOptionId == Guid.Empty || question.Options.All(option => option.Id != selectedOptionId))
        {
            throw new ExamAnswerOptionInvalidException();
        }

        return Create(
            attempt,
            attemptQuestion,
            question,
            revisionNumber,
            ExamQuestionType.ObjectiveSingleChoice,
            selectedOptionId,
            null,
            clientOperationId,
            requestHash,
            createdBySessionId,
            supersedesAnswerRevisionId,
            acceptedWriteLeaseVersion,
            acceptedAnswerSetVersion,
            acceptedAtUtc, attachmentFileIds);
    }

    public static AnswerRevision CreateDescriptive(
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        QuestionVersion question,
        string textAnswer,
        int revisionNumber,
        Guid clientOperationId,
        string requestHash,
        Guid createdBySessionId,
        Guid? supersedesAnswerRevisionId,
        long acceptedWriteLeaseVersion,
        long acceptedAnswerSetVersion,
        DateTime acceptedAtUtc,
        IReadOnlyCollection<Guid>? attachmentFileIds = null)
    {
        EnsureRelationship(attempt, attemptQuestion, question);
        if (question.Type != ExamQuestionType.Descriptive)
        {
            throw new ExamAnswerTypeInvalidException();
        }

        if (textAnswer is null || textAnswer.Length > MaximumTextAnswerLength)
        {
            throw new ExamAnswerTypeInvalidException();
        }

        return Create(
            attempt,
            attemptQuestion,
            question,
            revisionNumber,
            ExamQuestionType.Descriptive,
            null,
            textAnswer,
            clientOperationId,
            requestHash,
            createdBySessionId,
            supersedesAnswerRevisionId,
            acceptedWriteLeaseVersion,
            acceptedAnswerSetVersion,
            acceptedAtUtc, attachmentFileIds);
    }

    private static AnswerRevision Create(
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        QuestionVersion question,
        int revisionNumber,
        ExamQuestionType answerType,
        Guid? selectedOptionId,
        string? textAnswer,
        Guid clientOperationId,
        string requestHash,
        Guid createdBySessionId,
        Guid? supersedesAnswerRevisionId,
        long acceptedWriteLeaseVersion,
        long acceptedAnswerSetVersion,
        DateTime acceptedAtUtc,
        IReadOnlyCollection<Guid>? attachmentFileIds)
    {
        if (revisionNumber <= 0 || acceptedWriteLeaseVersion <= 0 || acceptedAnswerSetVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }

        if (clientOperationId == Guid.Empty || createdBySessionId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های ثبت پاسخ الزامی هستند.");
        }

        if (requestHash.Length != RequestHashLength)
        {
            throw new ArgumentException("هش درخواست پاسخ معتبر نیست.", nameof(requestHash));
        }

        if (acceptedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پذیرش پاسخ باید UTC باشد.", nameof(acceptedAtUtc));
        }

        var files = attachmentFileIds ?? [];
        if (files.Count > 0 && (question.AttachmentPolicy is not { } policy || files.Count > policy.MaximumCount ||
            files.Any(id => id == Guid.Empty) || files.Distinct().Count() != files.Count))
            throw new ExamAnswerTypeInvalidException();
        var revision = new AnswerRevision(
            Guid.NewGuid(),
            attempt.OrganizationId,
            attempt.Id,
            attemptQuestion.Id,
            question.Id,
            revisionNumber,
            answerType,
            selectedOptionId,
            textAnswer,
            clientOperationId,
            requestHash,
            createdBySessionId,
            supersedesAnswerRevisionId,
            acceptedWriteLeaseVersion,
            acceptedAnswerSetVersion,
            acceptedAtUtc);
        revision._attachments.AddRange(files.Select(id => AnswerRevisionAttachment.Create(revision, id)));
        return revision;
    }

    private static void EnsureRelationship(
        ExamAttempt attempt,
        ExamAttemptQuestion attemptQuestion,
        QuestionVersion question)
    {
        if (attemptQuestion.OrganizationId != attempt.OrganizationId ||
            attemptQuestion.ExamAttemptId != attempt.Id ||
            attemptQuestion.ExamVersionId != attempt.ExamVersionId ||
            attemptQuestion.QuestionVersionId != question.Id ||
            question.OrganizationId != attempt.OrganizationId ||
            question.ExamVersionId != attempt.ExamVersionId)
        {
            throw new InvalidOperationException("سؤال برای این تلاش آزمون تثبیت نشده است.");
        }
    }
}

public sealed class ExamAnswerTypeInvalidException : Exception;
public sealed class ExamAnswerOptionInvalidException : Exception;
