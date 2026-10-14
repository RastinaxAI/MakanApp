namespace MakanApp.Domain.Organization;

public sealed class OrganizationPerson
{
    private OrganizationPerson()
    {
    }

    private OrganizationPerson(
        Guid id,
        Guid organizationId,
        Guid personId,
        DateTime createdAtUtc,
        DateTime activatedAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        PersonId = personId;
        Status = OrganizationPersonStatus.Active;
        CreatedAtUtc = createdAtUtc;
        ActivatedAtUtc = activatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid PersonId { get; private set; }
    public OrganizationPersonStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ActivatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public Guid? CreationOperationId { get; private set; }
    public string? CreationRequestHash { get; private set; }

    public bool IsActive =>
        Status == OrganizationPersonStatus.Active && !EndedAtUtc.HasValue;

    public static OrganizationPerson CreateActive(
        Guid organizationId,
        Guid personId,
        DateTime createdAtUtc) =>
        new(Guid.NewGuid(), organizationId, personId, createdAtUtc, createdAtUtc);

    public static OrganizationPerson CreateOnboarded(
        Guid organizationId, Guid personId, Guid operationId, string requestHash, DateTime createdAtUtc)
    {
        if (operationId == Guid.Empty || requestHash.Length != 64)
            throw new ArgumentException("شناسه و اثر انگشت درخواست ایجاد معتبر نیست.");
        var person = CreateActive(organizationId, personId, createdAtUtc);
        person.CreationOperationId = operationId;
        person.CreationRequestHash = requestHash;
        return person;
    }

    public void LinkConsentingAccountPerson(Guid accountPersonId)
    {
        if (!IsActive || accountPersonId == Guid.Empty)
            throw new InvalidOperationException("پرونده فعال و شخص حساب معتبر برای اتصال لازم است.");
        // فقط مرجع پرونده سازمانی عوض می‌شود؛ هیچ Person یا سابقه‌ای merge یا حذف نمی‌شود.
        PersonId = accountPersonId;
    }

    public void End(DateTime endedAtUtc)
    {
        if (EndedAtUtc.HasValue)
        {
            return;
        }

        Status = OrganizationPersonStatus.Ended;
        EndedAtUtc = endedAtUtc;
    }
}
