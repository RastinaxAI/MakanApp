namespace MakanApp.Domain.Academic;

public sealed class PersonalClassInvitation
{
    private PersonalClassInvitation() { }
    public Guid Id { get; private set; }
    public Guid PersonalClassId { get; private set; }
    public Guid DestinationUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PersonalClassInvitation Create(Guid classId, Guid destination, Guid operationId, DateTime utcNow, DateTime expiresAtUtc)
    {
        if (classId == Guid.Empty || destination == Guid.Empty || operationId == Guid.Empty || utcNow.Kind != DateTimeKind.Utc
            || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= utcNow) throw new ArgumentException("دعوت کلاس مستقل معتبر نیست.");
        return new PersonalClassInvitation { Id = Guid.NewGuid(), PersonalClassId = classId, DestinationUserId = destination,
            ClientOperationId = operationId, CreatedAtUtc = utcNow, ExpiresAtUtc = expiresAtUtc };
    }

    public bool Accept(Guid userId, DateTime utcNow)
    {
        if (userId != DestinationUserId || RevokedAtUtc.HasValue || utcNow.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("دعوت معتبر و مجاز نیست.");
        if (AcceptedAtUtc.HasValue) return false;
        if (utcNow >= ExpiresAtUtc) throw new InvalidOperationException("دعوت منقضی شده است.");
        AcceptedAtUtc = utcNow; return true;
    }
}
