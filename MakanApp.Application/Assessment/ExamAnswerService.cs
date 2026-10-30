using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamAnswerService(
    IAccessContextResolver accessContextResolver,
    IExamAnswerStore store,
    TimeProvider timeProvider,
    IFileAssetStore fileStore,
    StorageOptions storageOptions) : IExamAnswerService
{
    public async Task<ExamWriteLeaseDto> AcquireWriteLeaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var nowUtc = UtcNow();
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var attempt = await store.GetOwnedAttemptForUpdateAsync(
            access.OrganizationId!.Value,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();
        try
        {
            attempt.AcquireWriteLease(sessionId, nowUtc);
        }
        catch (Exception exception) when (IsLeaseDomainException(exception))
        {
            throw MapDomainException(exception);
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToLease(attempt, sessionId, nowUtc);
    }

    public async Task<ExamWriteLeaseDto> TransferWriteLeaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var nowUtc = UtcNow();
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var attempt = await store.GetOwnedAttemptForUpdateAsync(
            access.OrganizationId!.Value,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();
        try
        {
            attempt.TransferWriteLease(sessionId, nowUtc);
        }
        catch (Exception exception) when (IsLeaseDomainException(exception))
        {
            throw MapDomainException(exception);
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToLease(attempt, sessionId, nowUtc);
    }

    public async Task<ExamAnswerReceiptDto> SaveAnswerAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid attemptQuestionId,
        SaveExamAnswerCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ClientOperationId == Guid.Empty)
        {
            throw Error(
                AssessmentErrorCodes.ExamAnswerIdempotencyConflict,
                "شناسه عملیات ذخیره پاسخ الزامی است.");
        }

        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var organizationId = access.OrganizationId!.Value;
        var nowUtc = UtcNow();
        var requestHash = ComputeRequestHash(attemptId, attemptQuestionId, command);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var attempt = await store.GetOwnedAttemptForUpdateAsync(
            organizationId,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();
        nowUtc = UtcNow();
        _ = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var idempotentRevision = await store.GetByClientOperationForUpdateAsync(
            organizationId,
            attemptId,
            command.ClientOperationId,
            cancellationToken);
        if (idempotentRevision is not null)
        {
            if (!string.Equals(idempotentRevision.RequestHash, requestHash, StringComparison.Ordinal))
            {
                throw Error(
                    AssessmentErrorCodes.ExamAnswerIdempotencyConflict,
                    "شناسه عملیات با payload دیگری قبلاً استفاده شده است.");
            }

            await transaction.CommitAsync(cancellationToken);
            return ToReceipt(idempotentRevision);
        }

        try
        {
            attempt.EnsureWriteLease(sessionId, command.WriteLeaseVersion, nowUtc);
        }
        catch (Exception exception) when (IsLeaseDomainException(exception))
        {
            throw MapDomainException(exception);
        }

        var answerQuestion = await store.GetQuestionForUpdateAsync(
            organizationId,
            attemptId,
            attemptQuestionId,
            cancellationToken) ?? throw Error(
                AssessmentErrorCodes.ExamAnswerNotAllowed,
                "سؤال برای این تلاش آزمون تثبیت نشده است.");
        if (answerQuestion.CurrentRevision?.RevisionNumber != command.ExpectedRevisionNumber)
        {
            throw Error(
                AssessmentErrorCodes.ExamAnswerVersionConflict,
                "نسخه پاسخ تغییر کرده است؛ پاسخ جاری را دوباره دریافت کنید.");
        }

        var nextRevisionNumber = checked((answerQuestion.CurrentRevision?.RevisionNumber ?? 0) + 1);
        var attachmentIds = answerQuestion.CurrentRevision?.Attachments.Select(item => item.FileAssetId).ToArray() ?? [];
        await ValidateAttachmentFilesAsync(access, attempt, answerQuestion.Question, attachmentIds, attachmentIds.ToHashSet(), UtcNow(), cancellationToken);
        var nextAnswerSetVersion = checked(attempt.AnswerSetVersion + 1);
        AnswerRevision revision;
        try
        {
            attempt.EnsureWriteLease(sessionId, command.WriteLeaseVersion, UtcNow());
            revision = CreateRevision(
                attempt,
                answerQuestion,
                command,
                requestHash,
                sessionId,
                nextRevisionNumber,
                nextAnswerSetVersion,
                UtcNow(), attachmentIds);
            var acceptedVersion = attempt.AcceptAnswerMutation();
            if (acceptedVersion != nextAnswerSetVersion)
            {
                throw new InvalidOperationException("نسخه مجموعه پاسخ‌ها به‌صورت اتمیک افزایش نیافت.");
            }

            answerQuestion.AttemptQuestion.AcceptAnswerRevision(revision);
        }
        catch (Exception exception) when (IsAnswerDomainException(exception))
        {
            throw MapDomainException(exception);
        }

        store.Add(revision);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToReceipt(revision);
    }

    public async Task<StudentExamAnswersDto> GetMyAnswersAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var access = await GetStudentContextAsync(userId, sessionId, cancellationToken);
        var nowUtc = UtcNow();
        var record = await store.GetOwnedAnswersAsync(
            access.OrganizationId!.Value,
            attemptId,
            userId,
            cancellationToken) ?? throw AttemptNotFound();
        return new StudentExamAnswersDto(
            record.Attempt.Id,
            record.Attempt.AnswerSetVersion,
            ToLease(record.Attempt, sessionId, nowUtc),
            record.Questions
                .Where(question => question.CurrentRevision is not null)
                .OrderBy(question => question.AttemptQuestion.DisplayOrder)
                .Select(question => ToCurrentAnswer(question))
                .ToArray());
    }

    private async Task<AccessContext> GetStudentContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != OrganizationRole.Student)
        {
            throw AttemptNotAllowed();
        }

        return context;
    }

    private static AnswerRevision CreateRevision(
        ExamAttempt attempt,
        ExamAnswerQuestionRecord answerQuestion,
        SaveExamAnswerCommand command,
        string requestHash,
        Guid sessionId,
        int revisionNumber,
        long answerSetVersion,
        DateTime acceptedAtUtc,
        IReadOnlyCollection<Guid>? attachmentIds = null)
    {
        if (answerQuestion.Question.Type == ExamQuestionType.ObjectiveSingleChoice)
        {
            if (!command.SelectedOptionId.HasValue || command.TextAnswer is not null)
            {
                throw new ExamAnswerTypeInvalidException();
            }

            return AnswerRevision.CreateObjective(
                attempt,
                answerQuestion.AttemptQuestion,
                answerQuestion.Question,
                command.SelectedOptionId.Value,
                revisionNumber,
                command.ClientOperationId,
                requestHash,
                sessionId,
                answerQuestion.CurrentRevision?.Id,
                attempt.WriteLeaseVersion,
                answerSetVersion,
                acceptedAtUtc, attachmentIds);
        }

        if (answerQuestion.Question.Type == ExamQuestionType.Descriptive)
        {
            if (command.SelectedOptionId.HasValue || command.TextAnswer is null && !answerQuestion.Question.AttachmentsAllowed)
            {
                throw new ExamAnswerTypeInvalidException();
            }

            return AnswerRevision.CreateDescriptive(
                attempt,
                answerQuestion.AttemptQuestion,
                answerQuestion.Question,
                command.TextAnswer ?? string.Empty,
                revisionNumber,
                command.ClientOperationId,
                requestHash,
                sessionId,
                answerQuestion.CurrentRevision?.Id,
                attempt.WriteLeaseVersion,
                answerSetVersion,
                acceptedAtUtc, attachmentIds);
        }

        throw new ExamAnswerTypeInvalidException();
    }

    private static ExamWriteLeaseDto ToLease(
        ExamAttempt attempt,
        Guid sessionId,
        DateTime nowUtc)
    {
        var writable = attempt.IsInProgress && nowUtc < attempt.EffectiveDeadlineUtc;
        var isWriter = writable && attempt.WriterSessionId == sessionId;
        return new ExamWriteLeaseDto(
            attempt.Id,
            isWriter,
            attempt.WriteLeaseVersion,
            writable && attempt.WriterSessionId.HasValue && !isWriter,
            attempt.WriteLeaseAcquiredAtUtc,
            attempt.AnswerSetVersion,
            nowUtc);
    }

    private static ExamAnswerReceiptDto ToReceipt(AnswerRevision revision) =>
        new(
            revision.ExamAttemptId,
            revision.ExamAttemptQuestionId,
            revision.Id,
            revision.RevisionNumber,
            revision.AcceptedAtUtc,
            revision.AcceptedWriteLeaseVersion,
            revision.AcceptedAnswerSetVersion,
            "Saved");

    private static StudentCurrentExamAnswerDto ToCurrentAnswer(ExamAnswerQuestionRecord record)
    {
        var attemptQuestion = record.AttemptQuestion;
        var revision = record.CurrentRevision!;
        return
        new(
            attemptQuestion.Id,
            attemptQuestion.QuestionVersionId,
            revision.Id,
            revision.RevisionNumber,
            revision.AnswerType,
            revision.SelectedOptionId,
            revision.TextAnswer,
            revision.AcceptedAtUtc)
        { Attachments = (record.AttachmentFiles ?? []).Select(item => AcademicAnswerAttachmentDto.From(item.FileAsset)).ToArray() };
    }

    private static string ComputeRequestHash(
        Guid attemptId,
        Guid attemptQuestionId,
        SaveExamAnswerCommand command)
    {
        var text = command.TextAnswer is null
            ? "<null>"
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(command.TextAnswer));
        var canonical = string.Join(
            '|',
            attemptId.ToString("N"),
            attemptQuestionId.ToString("N"),
            command.WriteLeaseVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            command.ExpectedRevisionNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>",
            command.SelectedOptionId?.ToString("N") ?? "<null>",
            text);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static bool IsLeaseDomainException(Exception exception) =>
        exception is ExamAttemptNotWritableException or ExamAttemptDeadlinePassedException or
            ExamWriteLeaseRequiredException or ExamWriteLeaseHeldException or ExamWriteLeaseStaleException;

    private static bool IsAnswerDomainException(Exception exception) =>
        IsLeaseDomainException(exception) ||
        exception is ExamAnswerTypeInvalidException or ExamAnswerOptionInvalidException or
            InvalidOperationException or ArgumentException or OverflowException;

    private static AssessmentException MapDomainException(Exception exception) => exception switch
    {
        ExamAttemptNotWritableException => Error(
            AssessmentErrorCodes.ExamAttemptNotWritable,
            "تلاش آزمون قابل ویرایش نیست."),
        ExamAttemptDeadlinePassedException => Error(
            AssessmentErrorCodes.ExamDeadlinePassed,
            "مهلت ثبت پاسخ آزمون پایان یافته است."),
        ExamWriteLeaseRequiredException => Error(
            AssessmentErrorCodes.ExamWriteLeaseRequired,
            "برای ذخیره پاسخ باید write lease فعال دریافت شود."),
        ExamWriteLeaseHeldException => Error(
            AssessmentErrorCodes.ExamWriteLeaseHeldByOtherSession,
            "نشست دیگری write lease این تلاش را در اختیار دارد."),
        ExamWriteLeaseStaleException => Error(
            AssessmentErrorCodes.ExamWriteLeaseStale,
            "نسخه write lease منقضی شده است؛ وضعیت جاری را دوباره دریافت کنید."),
        ExamAnswerOptionInvalidException => Error(
            AssessmentErrorCodes.ExamAnswerOptionInvalid,
            "گزینه انتخاب‌شده متعلق به سؤال این تلاش نیست."),
        ExamAnswerTypeInvalidException or ArgumentException => Error(
            AssessmentErrorCodes.ExamAnswerTypeInvalid,
            "ساختار پاسخ با نوع سؤال سازگار نیست."),
        InvalidOperationException => Error(
            AssessmentErrorCodes.ExamAnswerVersionConflict,
            "نسخه پاسخ با وضعیت جاری سازگار نیست."),
        _ => Error(AssessmentErrorCodes.ConcurrencyConflict, "تغییر هم‌زمان پاسخ پذیرفته نشد.")
    };

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static AssessmentException AttemptNotFound() =>
        Error(AssessmentErrorCodes.ExamAttemptNotFound, "تلاش آزمون پیدا نشد.");

    private static AssessmentException AttemptNotAllowed() =>
        Error(AssessmentErrorCodes.ExamAttemptNotAllowed, "دسترسی به تلاش آزمون مجاز نیست.");

    private static AssessmentException Error(string code, string message) => new(code, message);
}
