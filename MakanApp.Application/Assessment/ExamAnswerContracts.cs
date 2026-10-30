using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed record SaveExamAnswerCommand(
    Guid ClientOperationId,
    long WriteLeaseVersion,
    int? ExpectedRevisionNumber,
    Guid? SelectedOptionId,
    string? TextAnswer);

public sealed record ExamWriteLeaseDto(
    Guid ExamAttemptId,
    bool IsCurrentSessionWriter,
    long WriteLeaseVersion,
    bool CanRequestTransfer,
    DateTime? AcquiredAtUtc,
    long AnswerSetVersion,
    DateTime ServerNowUtc);

public sealed record ExamAnswerReceiptDto(
    Guid ExamAttemptId,
    Guid ExamAttemptQuestionId,
    Guid AnswerRevisionId,
    int RevisionNumber,
    DateTime AcceptedAtUtc,
    long WriteLeaseVersion,
    long AnswerSetVersion,
    string Status);

public sealed record StudentCurrentExamAnswerDto(
    Guid ExamAttemptQuestionId,
    Guid QuestionVersionId,
    Guid AnswerRevisionId,
    int RevisionNumber,
    ExamQuestionType AnswerType,
    Guid? SelectedOptionId,
    string? TextAnswer,
    DateTime AcceptedAtUtc)
{
    public IReadOnlyCollection<AcademicAnswerAttachmentDto> Attachments { get; init; } = [];
}

public sealed record StudentExamAnswersDto(
    Guid ExamAttemptId,
    long AnswerSetVersion,
    ExamWriteLeaseDto WriteLease,
    IReadOnlyCollection<StudentCurrentExamAnswerDto> Answers);

public sealed record ExamAnswerQuestionRecord(
    ExamAttemptQuestion AttemptQuestion,
    QuestionVersion Question,
    AnswerRevision? CurrentRevision,
    IReadOnlyCollection<ExamAnswerAttachmentRecord>? AttachmentFiles = null);

public sealed record ExamAnswersRecord(
    ExamAttempt Attempt,
    IReadOnlyCollection<ExamAnswerQuestionRecord> Questions);
