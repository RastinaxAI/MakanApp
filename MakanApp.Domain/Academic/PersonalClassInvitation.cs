namespace MakanApp.Domain.Academic;

public sealed class PersonalClassInvitation
{
    private PersonalClassInvitation() { }
    public Guid Id { get; private set; }
    public Guid PersonalClassId { get; private set; }
    public Guid DestinationUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public PersonalClassLocalRoles LocalRoles { get; private set; } = PersonalClassLocalRoles.Learner;
    public PersonalClassDelegatedCapabilities DelegatedCapabilities { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime? DeclinedAtUtc { get; private set; }
    public string? ClosureReason { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PersonalClassInvitation Create(Guid classId, Guid destination, Guid operationId, DateTime utcNow, DateTime expiresAtUtc,
        PersonalClassLocalRoles roles = PersonalClassLocalRoles.Learner, PersonalClassDelegatedCapabilities capabilities = PersonalClassDelegatedCapabilities.None)
    {
        if (classId == Guid.Empty || destination == Guid.Empty || operationId == Guid.Empty || utcNow.Kind != DateTimeKind.Utc
            || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= utcNow) throw new ArgumentException("دعوت کلاس مستقل معتبر نیست.");
        if (roles.HasFlag(PersonalClassLocalRoles.Manager)) throw new ArgumentException("مدیریت نیازمند انتقال مسئولیت است.");
        PersonalClassParticipant.ValidateRoles(roles, capabilities);
        return new PersonalClassInvitation { Id = Guid.NewGuid(), PersonalClassId = classId, DestinationUserId = destination,
            ClientOperationId = operationId, CreatedAtUtc = utcNow, ExpiresAtUtc = expiresAtUtc, LocalRoles = roles, DelegatedCapabilities = capabilities };
    }

    public bool Accept(Guid userId, DateTime utcNow)
    {
        if (userId != DestinationUserId || RevokedAtUtc.HasValue || DeclinedAtUtc.HasValue || utcNow.Kind != DateTimeKind.Utc || utcNow < CreatedAtUtc)
            throw new InvalidOperationException("دعوت معتبر و مجاز نیست.");
        if (AcceptedAtUtc.HasValue) return false;
        if (utcNow >= ExpiresAtUtc) throw new InvalidOperationException("دعوت منقضی شده است.");
        AcceptedAtUtc = utcNow; return true;
    }

    public string StatusAt(DateTime nowUtc) => AcceptedAtUtc.HasValue ? "Accepted" : DeclinedAtUtc.HasValue ? "Declined" :
        RevokedAtUtc.HasValue ? "Revoked" : nowUtc >= ExpiresAtUtc ? "Expired" : "Pending";

    public void Close(bool decline, string reason, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc || nowUtc < CreatedAtUtc || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("دلیل و زمان معتبر لازم است.");
        if (StatusAt(nowUtc) != "Pending") throw new InvalidOperationException("دعوت باز نیست.");
        if (decline) DeclinedAtUtc = nowUtc; else RevokedAtUtc = nowUtc;
        ClosureReason = reason.Trim();
    }
}
