namespace MakanApp.Domain.Knowledge;

public enum EducationalSourceScope { Personal = 1, Class = 2, Organization = 3, Library = 4 }
public enum EducationalSourceStatus { Active = 1, Inactive = 2, Archived = 3 }
public enum SourcePublicationStatus { Draft = 1, Published = 2 }
public enum SourceVisibility { Hidden = 1, Public = 2 }
public enum EducationalSourceType { Document = 1, Image = 2 }
public enum SourceSafetyStatus { PendingValidation = 1, FileValidated = 2, ReviewRequired = 3, Rejected = 4 }
public enum SourceProcessingStatus { NotRequested = 1, Pending = 2, Processing = 3, Ready = 4, Failed = 5 }

public sealed class EducationalSource
{
    private EducationalSource() { }
    public Guid Id { get; private set; }
    public EducationalSourceScope Scope { get; private set; }
    public Guid? OwnerUserId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? ClassId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string CreationRequestHash { get; private set; } = string.Empty;
    public EducationalSourceStatus Status { get; private set; }
    public SourceVisibility Visibility { get; private set; }
    public int CurrentVersion { get; private set; }
    public int? PublishedVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static EducationalSource Create(EducationalSourceScope scope, Guid actor, Guid? organizationId, Guid? classId,
        Guid operation, string hash, DateTime now)
    {
        if (!Enum.IsDefined(scope) || actor == Guid.Empty || operation == Guid.Empty || hash.Length != 64 || now.Kind != DateTimeKind.Utc ||
            organizationId == Guid.Empty || classId == Guid.Empty ||
            (scope == EducationalSourceScope.Class ? !organizationId.HasValue || !classId.HasValue : classId.HasValue) ||
            (scope == EducationalSourceScope.Organization ? !organizationId.HasValue : scope != EducationalSourceScope.Class && organizationId.HasValue))
            throw new ArgumentException("مالک، بافت یا مشخصات منبع معتبر نیست.");
        return new() { Id = Guid.NewGuid(), Scope = scope, OwnerUserId = scope == EducationalSourceScope.Personal ? actor : null,
            OrganizationId = organizationId, ClassId = classId, CreatedByUserId = actor, ClientOperationId = operation,
            CreationRequestHash = hash, Status = EducationalSourceStatus.Active, Visibility = SourceVisibility.Hidden,
            CurrentVersion = 1, CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    public void Touch(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || Status != EducationalSourceStatus.Active) throw new InvalidOperationException("منبع قابل ویرایش نیست.");
        UpdatedAtUtc = now;
    }
    public void AdvanceVersion(DateTime now)
    {
        Touch(now); CurrentVersion = checked(CurrentVersion + 1);
    }
    public void Publish(EducationalSourceVersion version, DateTime now)
    {
        if (version.SourceId != Id || version.Number != CurrentVersion || version.PublicationStatus != SourcePublicationStatus.Published)
            throw new InvalidOperationException("نسخهٔ انتشار معتبر نیست.");
        Touch(now); PublishedVersion = version.Number;
    }
    public void SetVisibility(SourceVisibility visibility, DateTime now)
    {
        if (Scope != EducationalSourceScope.Library || !Enum.IsDefined(visibility)) throw new ArgumentException("نمایانی منبع معتبر نیست.");
        Touch(now); Visibility = visibility;
    }
    public void Archive(DateTime now)
    {
        Touch(now); Status = EducationalSourceStatus.Archived; ArchivedAtUtc = now;
    }
}
