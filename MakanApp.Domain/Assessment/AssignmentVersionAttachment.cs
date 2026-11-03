namespace MakanApp.Domain.Assessment;

public sealed class AssignmentVersionAttachment : IAcademicScoped
{
    private AssignmentVersionAttachment() { }
    internal AssignmentVersionAttachment(AssignmentVersion version, Guid fileAssetId, DateTime now)
    {
        if (version.IsPublished || fileAssetId == Guid.Empty) throw new InvalidOperationException("پیوست فقط به نسخهٔ پیش‌نویس افزوده می‌شود.");
        Id = Guid.NewGuid(); ContextKind = version.ContextKind; ContextId = version.ContextId;
        OrganizationId = version.OrganizationId; PersonalClassId = version.PersonalClassId;
        AssignmentId = version.AssignmentId; AssignmentVersionId = version.Id; FileAssetId = fileAssetId; AttachedAtUtc = now;
    }
    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid AssignmentId { get; private set; }
    public Guid AssignmentVersionId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public DateTime AttachedAtUtc { get; private set; }
}
