using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Assessment;

internal static class ExamAnswerAttachmentValidator
{
    private static AssessmentException Error(string code, string message) => new(code, message);
    internal static async Task ValidateAsync(IFileAssetStore fileStore, IExamAnswerStore store, StorageOptions storageOptions,
        AccessContext access, ExamAttempt attempt, QuestionVersion question,
        IReadOnlyCollection<Guid> files, IReadOnlySet<Guid> existing, DateTime now, CancellationToken ct)
    {
        var policy = question.AttachmentPolicy;
        if (files.Count > 0 && policy is null) throw Error(AssessmentErrorCodes.ExamAttachmentNotAllowed, "پیوست برای این سؤال مجاز نیست.");
        if (policy is not null && files.Count > policy.MaximumCount) throw AttachmentLimit();
        long total = 0;
        foreach (var id in files.Order())
        {
            var file = await fileStore.GetForUpdateAsync(id, ct) ?? throw FileDenied();
            if (file.UploadedByUserId != access.UserId || file.OrganizationId != attempt.OrganizationId ||
                !await store.IsFileUsableForAttemptAsync(id, attempt.Id, ct)) throw FileDenied();
            if (file.Status != FileAssetStatus.Ready || string.IsNullOrEmpty(file.Sha256Hash) ||
                (!existing.Contains(id) && file.RetainedAtUtc is null && file.UnattachedExpiresAtUtc <= now)) throw FileNotReady();
            if (policy is null || !policy.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase) ||
                !storageOptions.AllowedContentTypes.Contains(file.ContentType) || file.SizeBytes > storageOptions.MaxFileSizeBytes)
                throw Error(AssessmentErrorCodes.ExamAttachmentNotAllowed, "نوع یا اندازه فایل با سیاست سؤال و Storage سازگار نیست.");
            try { total = checked(total + file.SizeBytes); } catch (OverflowException) { throw AttachmentLimit(); }
            if (total > policy.MaximumTotalBytes) throw AttachmentLimit();
            file.Retain(now);
        }
    }

    private static AssessmentException AttachmentLimit() => Error(AssessmentErrorCodes.ExamAttachmentLimitExceeded, "سقف تعداد یا مجموع بایت پیوست‌های سؤال رعایت نشده است.");
    private static StorageException FileDenied() => new(StorageErrorCodes.FileNotFound, "فایل مجاز پیدا نشد.");
    private static StorageException FileNotReady() => new(StorageErrorCodes.FileNotReady, "فایل برای اتصال یا نهایی‌سازی آماده نیست.");
}
