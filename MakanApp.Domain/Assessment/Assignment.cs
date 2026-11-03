namespace MakanApp.Domain.Assessment;

public sealed class Assignment : IAcademicScoped
{
    private Assignment()
    {
    }

    private Assignment(
        Guid id,
        AcademicScope scope,
        Guid classId,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = scope.OrganizationId;
        PersonalClassId = scope.PersonalClassId;
        ContextKind = scope.Kind;
        ContextId = scope.Id;
        scope.EnsureClass(classId);
        ClassId = classId;
        CreatedByMembershipId = Scope.Kind == AcademicClassKind.OrganizationClass ? createdByMembershipId : null;
        CreatedByPersonalMembershipId = Scope.Kind == AcademicClassKind.PersonalClass ? createdByMembershipId : null;
        CurrentVersionNumber = 1;
        Status = AssignmentStatus.Draft;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? PersonalClassId { get; private set; }
    public AcademicClassKind ContextKind { get; private set; }
    public Guid ContextId { get; private set; }
    public AcademicScope Scope => new(ContextKind, ContextId);
    public Guid ClassId { get; private set; }
    public AcademicClassRef ClassRef => new(Scope, ClassId);
    public Guid? CreatedByMembershipId { get; private set; }
    public Guid? CreatedByPersonalMembershipId { get; private set; }
    public int CurrentVersionNumber { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Status == AssignmentStatus.Draft;
    public bool IsRecipientVisible => Status is AssignmentStatus.Published or AssignmentStatus.Closed;

    public static Assignment CreateDraft(
        AcademicScope scope,
        Guid classId,
        Guid createdByMembershipId,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            scope,
            classId,
            createdByMembershipId,
            createdAtUtc);

    public void Publish(DateTime publishedAtUtc)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("فقط تکلیف پیش‌نویس قابل انتشار است.");
        }

        Status = AssignmentStatus.Published;
        PublishedAtUtc = publishedAtUtc;
        UpdatedAtUtc = publishedAtUtc;
    }

    public void MarkDraftUpdated(DateTime updatedAtUtc)
    {
        if (!IsDraft)
        {
            throw new InvalidOperationException("فقط تکلیف پیش‌نویس قابل ویرایش است.");
        }

        UpdatedAtUtc = updatedAtUtc;
    }
}
