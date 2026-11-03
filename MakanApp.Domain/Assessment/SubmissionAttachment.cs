namespace MakanApp.Domain.Assessment;

public sealed class SubmissionAttachment : IAcademicScoped
{
    private SubmissionAttachment()
    {
    }

    private SubmissionAttachment(
        Guid id,
        AcademicScope scope,
        Guid submissionAttemptId,
        Guid fileAssetId,
        DateTime attachedAtUtc)
    {
        if (id == Guid.Empty || scope.Id == Guid.Empty ||
            submissionAttemptId == Guid.Empty || fileAssetId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های پیوست ارسال الزامی هستند.");
        }

        if (attachedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان اتصال فایل باید UTC باشد.", nameof(attachedAtUtc));
        }

        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        SubmissionAttemptId = submissionAttemptId;
        FileAssetId = fileAssetId;
        AttachedAtUtc = attachedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid SubmissionAttemptId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public DateTime AttachedAtUtc { get; private set; }

    public static SubmissionAttachment Create(
        AcademicScope scope,
        Guid submissionAttemptId,
        Guid fileAssetId,
        DateTime attachedAtUtc) =>
        new(Guid.NewGuid(), scope, submissionAttemptId, fileAssetId, attachedAtUtc);
}
