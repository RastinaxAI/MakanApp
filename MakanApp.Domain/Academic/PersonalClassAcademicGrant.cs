namespace MakanApp.Domain.Academic;

[Flags]
public enum PersonalClassAcademicCapabilities
{
    None = 0,
    AuthorAssignments = 1,
    AuthorExams = 2,
    Grade = 4
}

/// <summary>مجوز صریح یک دورهٔ عضویت؛ تفویض به نسخهٔ مشخص مجوز مدیر وابسته است.</summary>
public sealed class PersonalClassAcademicGrant
{
    private PersonalClassAcademicGrant() { }
    public Guid Id { get; private set; }
    public Guid PersonalClassId { get; private set; }
    public Guid MembershipId { get; private set; }
    public PersonalClassAcademicCapabilities Capabilities { get; private set; }
    public Guid? ParentGrantId { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PersonalClassAcademicGrant Create(Guid classId, Guid membershipId,
        PersonalClassAcademicCapabilities capabilities, Guid? parentGrantId, Guid actorId,
        Guid operationId, string requestHash, DateTime now)
    {
        if (classId == Guid.Empty || membershipId == Guid.Empty || actorId == Guid.Empty || operationId == Guid.Empty ||
            parentGrantId == Guid.Empty || requestHash.Length != 64 || now.Kind != DateTimeKind.Utc ||
            ((int)capabilities & ~7) != 0)
            throw new ArgumentException("مجوز آموزشی معتبر نیست.");
        return new() { Id = Guid.NewGuid(), PersonalClassId = classId, MembershipId = membershipId,
            Capabilities = capabilities, ParentGrantId = parentGrantId, GrantedByUserId = actorId,
            ClientOperationId = operationId, RequestHash = requestHash, CreatedAtUtc = now,
            RevokedAtUtc = capabilities == PersonalClassAcademicCapabilities.None ? now : null };
    }

    public void Revoke(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || now < CreatedAtUtc) throw new ArgumentException("زمان لغو معتبر نیست.");
        RevokedAtUtc ??= now;
    }

    public bool Allows(PersonalClassAcademicCapabilities capability) =>
        RevokedAtUtc is null && capability != PersonalClassAcademicCapabilities.None && (Capabilities & capability) == capability;
}
