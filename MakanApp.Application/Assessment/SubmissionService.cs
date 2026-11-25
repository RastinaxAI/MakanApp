using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Assessment;

public sealed partial class SubmissionService : ISubmissionService
{
    private readonly IAcademicContextResolver _accessContextResolver;
    private readonly ISubmissionStore _store;
    private readonly IFileAssetStore _fileAssetStore;
    private readonly TimeProvider _timeProvider;

    public SubmissionService(
        IAcademicContextResolver accessContextResolver,
        ISubmissionStore store,
        IFileAssetStore fileAssetStore,
        TimeProvider timeProvider)
    {
        _accessContextResolver = accessContextResolver;
        _store = store;
        _fileAssetStore = fileAssetStore;
        _timeProvider = timeProvider;
    }

    public async Task<SubmissionAttemptResult> CreateOrResumeDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        var context = await GetStudentContextAsync(userId, sessionId, AcademicResourceKind.Assignment, assignmentId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var eligibility = await _store.GetEligibilityForStudentForUpdateAsync(
            context.Scope,
            assignmentId,
            context.UserId,
            cancellationToken) ?? throw RecipientNotFound();
        EnsureEligibleForMutation(eligibility);
        EnsureDeadlineAllowsMutation(eligibility.Assignment, eligibility.Version, null, UtcNow());

        var existingDraft = await _store.GetDraftForUpdateAsync(
            eligibility.Recipient.Scope,
            eligibility.Recipient.Id,
            eligibility.Version.Id,
            cancellationToken);
        if (existingDraft is not null)
        {
            var existingRecord = await _store.GetAttemptForUpdateAsync(
                eligibility.Recipient.Scope,
                existingDraft.Id,
                cancellationToken) ?? throw SubmissionNotFound();
            await transaction.CommitAsync(cancellationToken);
            return ToResult(existingRecord, true);
        }

        var submittedCount = await _store.CountSubmittedAttemptsForUpdateAsync(
            eligibility.Recipient.Scope,
            eligibility.Recipient.Id,
            eligibility.Version.Id,
            cancellationToken);
        if (submittedCount >= eligibility.Version.MaxAttempts)
        {
            throw Error(
                AssessmentErrorCodes.SubmissionAttemptsExhausted,
                "تعداد تلاش‌های مجاز این تکلیف تمام شده است.");
        }

        var createdAtUtc = UtcNow();
        EnsureDeadlineAllowsMutation(eligibility.Assignment, eligibility.Version, null, createdAtUtc);
        var attempt = SubmissionAttempt.CreateDraft(
            eligibility.Recipient.Scope,
            eligibility.Assignment.Id,
            eligibility.Version.Id,
            eligibility.Recipient.Id,
            eligibility.Participation.Reference,
            submittedCount + 1,
            createdAtUtc);
        _store.Add(attempt);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(ToRecord(attempt, eligibility), true);
    }

    public async Task<SubmissionAttemptResult> SaveDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        SaveSubmissionDraftCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetStudentContextAsync(userId, sessionId, AcademicResourceKind.SubmissionAttempt, attemptId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var record = await GetOwnedAttemptForUpdateAsync(context, attemptId, cancellationToken);
        EnsureDraft(record.Attempt);
        EnsureEligibleForMutation(record);
        var savedAtUtc = UtcNow();
        EnsureDeadlineAllowsMutation(record.Assignment, record.Version, record.Attempt, savedAtUtc);
        ApplyExpectedRowVersion(record.Attempt, command.ExpectedRowVersion);

        try
        {
            record.Attempt.SaveAnswer(command.AnswerText, savedAtUtc);
        }
        catch (ArgumentException exception)
        {
            throw Error(AssessmentErrorCodes.SubmissionAnswerInvalid, exception.Message);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(record, true);
    }

    public async Task<SubmissionAttemptResult> AttachFileAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        AttachSubmissionFileCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetStudentContextAsync(userId, sessionId, AcademicResourceKind.SubmissionAttempt, attemptId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var record = await GetOwnedAttemptForUpdateAsync(context, attemptId, cancellationToken);
        EnsureDraft(record.Attempt);
        EnsureEligibleForMutation(record);
        EnsureDeadlineAllowsMutation(record.Assignment, record.Version, record.Attempt, UtcNow());

        if (record.Attachments.Any(item => item.FileAsset.Id == command.FileAssetId))
        {
            await transaction.CommitAsync(cancellationToken);
            return ToResult(record, true);
        }

        ApplyExpectedRowVersion(record.Attempt, command.ExpectedRowVersion);
        var fileAsset = await _fileAssetStore.GetForUpdateAsync(
            command.FileAssetId,
            cancellationToken) ?? throw FileNotAllowed();
        EnsureFileCanBeAttached(fileAsset, context);
        if (!await _store.IsFileAvailableForAttemptAsync(fileAsset.Id, record.Attempt.Id, cancellationToken)) throw FileNotAllowed();

        // انتظار برای قفل فایل ممکن است از مرز مهلت عبور کند؛ زمان پذیرش پس از گرفتن قفل ملاک است.
        var attachedAtUtc = UtcNow();
        EnsureDeadlineAllowsMutation(record.Assignment, record.Version, record.Attempt, attachedAtUtc);
        var attachment = SubmissionAttachment.Create(
            record.Attempt.Scope,
            record.Attempt.Id,
            fileAsset.Id,
            attachedAtUtc);
        record.Attempt.MarkAttachmentsChanged(attachedAtUtc);
        _store.Add(attachment);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var attachments = record.Attachments
            .Append(new SubmissionAttachmentWithFileRecord(attachment, fileAsset))
            .ToArray();
        return ToResult(record with { Attachments = attachments }, true);
    }

    public async Task<SubmissionAttemptResult> RemoveFileAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid fileAssetId,
        string expectedRowVersion,
        CancellationToken cancellationToken)
    {
        var context = await GetStudentContextAsync(userId, sessionId, AcademicResourceKind.SubmissionAttempt, attemptId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var record = await GetOwnedAttemptForUpdateAsync(context, attemptId, cancellationToken);
        EnsureDraft(record.Attempt);
        EnsureEligibleForMutation(record);
        var changedAtUtc = UtcNow();
        EnsureDeadlineAllowsMutation(record.Assignment, record.Version, record.Attempt, changedAtUtc);
        var existing = record.Attachments.SingleOrDefault(item => item.FileAsset.Id == fileAssetId);
        if (existing is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToResult(record, true);
        }

        ApplyExpectedRowVersion(record.Attempt, expectedRowVersion);
        record.Attempt.MarkAttachmentsChanged(changedAtUtc);
        _store.Remove(existing.Attachment);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(
            record with { Attachments = record.Attachments.Where(item => item != existing).ToArray() },
            true);
    }

    public async Task<SubmissionReceipt> FinalSubmitAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        FinalSubmitAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetStudentContextAsync(userId, sessionId, AcademicResourceKind.SubmissionAttempt, attemptId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        context = await _accessContextResolver.RevalidateAsync(context, cancellationToken);
        var record = await GetOwnedAttemptForUpdateAsync(context, attemptId, cancellationToken);
        if (record.Attempt.Status == SubmissionAttemptStatus.Submitted)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToReceipt(record.Attempt);
        }

        EnsureEligibleForMutation(record);
        ApplyExpectedRowVersion(record.Attempt, command.ExpectedRowVersion);
        var submittedCount = await _store.CountSubmittedAttemptsForUpdateAsync(
            record.Attempt.Scope,
            record.Attempt.AssignmentRecipientId,
            record.Attempt.AssignmentVersionId,
            cancellationToken);
        if (submittedCount >= record.Version.MaxAttempts ||
            record.Attempt.AttemptNumber > record.Version.MaxAttempts)
        {
            throw Error(
                AssessmentErrorCodes.SubmissionAttemptsExhausted,
                "تعداد تلاش‌های مجاز این تکلیف تمام شده است.");
        }

        var retainedFiles = new List<FileAsset>(record.Attachments.Count);
        foreach (var attachment in record.Attachments)
        {
            var fileAsset = await _fileAssetStore.GetForUpdateAsync(
                attachment.FileAsset.Id,
                cancellationToken) ?? throw FileNotReady();
            EnsureFileCanBeFinalized(fileAsset, context);
            retainedFiles.Add(fileAsset);
        }

        var submittedAtUtc = UtcNow();
        try
        {
            record.Attempt.Submit(
                submittedAtUtc,
                record.Version.DueAtUtc,
                record.Version.AllowLateSubmission,
                retainedFiles.Count > 0);
        }
        catch (SubmissionDeadlinePassedException)
        {
            throw DeadlinePassed(record.Assignment, record.Version, record.Attempt, submittedAtUtc);
        }
        catch (SubmissionEmptyException)
        {
            throw Error(
                AssessmentErrorCodes.SubmissionEmpty,
                "برای ارسال نهایی، متن پاسخ یا حداقل یک فایل آماده لازم است.");
        }

        foreach (var fileAsset in retainedFiles)
        {
            fileAsset.Retain(submittedAtUtc);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToReceipt(record.Attempt);
    }
}
