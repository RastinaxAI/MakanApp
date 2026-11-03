namespace MakanApp.Domain.Assessment;

public sealed class AnswerRevisionAttachment : IAcademicScoped
{
    private AnswerRevisionAttachment() { }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamAttemptQuestionId { get; private set; }
    public Guid AnswerRevisionId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public DateTime AttachedAtUtc { get; private set; }

    internal static AnswerRevisionAttachment Create(AnswerRevision revision, Guid fileAssetId)
    {
        if (fileAssetId == Guid.Empty) throw new ArgumentException("شناسه فایل معتبر نیست.");
        return new() { OrganizationId = revision.OrganizationId, PersonalClassId = revision.PersonalClassId, ContextKind = revision.ContextKind, ContextId = revision.ContextId, ExamAttemptId = revision.ExamAttemptId,
            ExamAttemptQuestionId = revision.ExamAttemptQuestionId, AnswerRevisionId = revision.Id,
            FileAssetId = fileAssetId, AttachedAtUtc = revision.AcceptedAtUtc };
    }
}
