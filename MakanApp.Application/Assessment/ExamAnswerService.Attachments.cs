using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamAnswerService
{
    public Task<ExamAnswerReceiptDto> AttachFileAsync(Guid userId, Guid sessionId, Guid attemptId, Guid questionId,
        AttachExamAnswerFileCommand command, CancellationToken ct) =>
        ChangeAttachmentAsync(userId, sessionId, attemptId, questionId, command.FileAssetId,
            new(command.ClientOperationId, command.WriteLeaseVersion, command.ExpectedRevisionNumber), true, ct);

    public Task<ExamAnswerReceiptDto> DetachFileAsync(Guid userId, Guid sessionId, Guid attemptId, Guid questionId,
        Guid fileAssetId, ChangeExamAnswerAttachmentCommand command, CancellationToken ct) =>
        ChangeAttachmentAsync(userId, sessionId, attemptId, questionId, fileAssetId, command, false, ct);

    private async Task<ExamAnswerReceiptDto> ChangeAttachmentAsync(Guid userId, Guid sessionId, Guid attemptId, Guid questionId,
        Guid fileAssetId, ChangeExamAnswerAttachmentCommand command, bool attach, CancellationToken ct)
    {
        if (command.ClientOperationId == Guid.Empty || fileAssetId == Guid.Empty)
            throw Error(AssessmentErrorCodes.ExamAttachmentNotAllowed, "شناسه عملیات و فایل الزامی است.");
        _ = await GetStudentContextAsync(userId, sessionId, ct);
        await using var tx = await store.BeginSerializableTransactionAsync(ct);
        var access = await GetStudentContextAsync(userId, sessionId, ct);
        var attempt = await store.GetOwnedAttemptForUpdateAsync(access.OrganizationId!.Value, attemptId, userId, ct) ?? throw AttemptNotFound();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', "exam-attachment-v1",
            attach ? "attach" : "detach", attemptId.ToString("N"), questionId.ToString("N"), fileAssetId.ToString("N"),
            command.WriteLeaseVersion.ToString(CultureInfo.InvariantCulture), command.ExpectedRevisionNumber?.ToString(CultureInfo.InvariantCulture) ?? "<null>"))));
        var previous = await store.GetByClientOperationForUpdateAsync(access.OrganizationId.Value, attemptId, command.ClientOperationId, ct);
        if (previous is not null)
        {
            if (previous.RequestHash != hash) throw Error(AssessmentErrorCodes.ExamAnswerIdempotencyConflict, "شناسه عملیات با درخواست دیگری استفاده شده است.");
            await tx.CommitAsync(ct); return ToReceipt(previous);
        }
        try { attempt.EnsureWriteLease(sessionId, command.WriteLeaseVersion, UtcNow()); }
        catch (Exception e) when (IsLeaseDomainException(e)) { throw MapDomainException(e); }
        var record = await store.GetQuestionForUpdateAsync(access.OrganizationId.Value, attemptId, questionId, ct)
            ?? throw Error(AssessmentErrorCodes.ExamAnswerNotAllowed, "سؤال متعلق به این تلاش نیست.");
        if (record.CurrentRevision?.RevisionNumber != command.ExpectedRevisionNumber)
            throw Error(AssessmentErrorCodes.ExamAnswerVersionConflict, "نسخه پاسخ تغییر کرده است؛ پاسخ جاری را دوباره دریافت کنید.");
        if (!record.Question.AttachmentsAllowed) throw Error(AssessmentErrorCodes.ExamAttachmentNotAllowed, "سیاست ثابت سؤال پیوست را مجاز نمی‌داند.");
        var existing = record.CurrentRevision?.Attachments.Select(item => item.FileAssetId).ToHashSet() ?? [];
        var files = existing.ToHashSet();
        if (attach) files.Add(fileAssetId); else files.Remove(fileAssetId);
        await ValidateAttachmentFilesAsync(access, attempt, record.Question, files, existing, UtcNow(), ct);
        var save = new SaveExamAnswerCommand(command.ClientOperationId, command.WriteLeaseVersion, command.ExpectedRevisionNumber,
            record.CurrentRevision?.SelectedOptionId, record.CurrentRevision?.TextAnswer ?? (record.Question.Type == ExamQuestionType.Descriptive ? string.Empty : null));
        AnswerRevision revision;
        try
        {
            attempt.EnsureWriteLease(sessionId, command.WriteLeaseVersion, UtcNow());
            var nextSet = checked(attempt.AnswerSetVersion + 1);
            revision = CreateRevision(attempt, record, save, hash, sessionId,
                checked((record.CurrentRevision?.RevisionNumber ?? 0) + 1), nextSet, UtcNow(), files.Order().ToArray());
            attempt.AcceptAnswerMutation(); record.AttemptQuestion.AcceptAnswerRevision(revision);
        }
        catch (Exception e) when (IsAnswerDomainException(e)) { throw MapDomainException(e); }
        store.Add(revision); await store.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ToReceipt(revision);
    }

    private Task ValidateAttachmentFilesAsync(AccessContext access, ExamAttempt attempt, QuestionVersion question,
        IReadOnlyCollection<Guid> files, IReadOnlySet<Guid> existing, DateTime now, CancellationToken ct) =>
        ExamAnswerAttachmentValidator.ValidateAsync(fileStore, store, storageOptions, access, attempt, question, files, existing, now, ct);

}
