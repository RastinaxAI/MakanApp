using MakanApp.Domain.Organization;

namespace MakanApp.Domain.Commerce;

// این نگاشت، دادهٔ صریح Platform است؛ هیچ قیمت یا سهمیهٔ پیش‌فرضی ایجاد نمی‌کند.
public sealed class SchoolEstablishmentOffering
{
    private SchoolEstablishmentOffering() { }
    public SchoolApplicationKind Kind { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public long Version { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static SchoolEstablishmentOffering Create(SchoolApplicationKind kind, Guid versionId, DateTime now)
    {
        if (!Enum.IsDefined(kind) || versionId == Guid.Empty || now.Kind != DateTimeKind.Utc) throw new ArgumentException("نگاشت خدمت معتبر نیست.");
        return new() { Kind = kind, PlanVersionId = versionId, Version = 1, UpdatedAtUtc = now };
    }
    public void Change(Guid versionId, DateTime now)
    {
        if (versionId == Guid.Empty || now.Kind != DateTimeKind.Utc) throw new ArgumentException("نگاشت خدمت معتبر نیست.");
        PlanVersionId = versionId; Version++; UpdatedAtUtc = now;
    }
}

public sealed class SchoolOfferingOperation
{
    private SchoolOfferingOperation() { }
    public Guid ActorUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public SchoolApplicationKind Kind { get; private set; }
    public Guid PlanVersionId { get; private set; }
    public long OfferingVersion { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public static SchoolOfferingOperation Create(Guid actor, Guid operation, SchoolEstablishmentOffering offering, string fingerprint) => new()
    { ActorUserId = actor, ClientOperationId = operation, Kind = offering.Kind, PlanVersionId = offering.PlanVersionId, OfferingVersion = offering.Version, Fingerprint = fingerprint };
}
