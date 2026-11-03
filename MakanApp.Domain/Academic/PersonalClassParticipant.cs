namespace MakanApp.Domain.Academic;

[Flags]
public enum PersonalClassLocalRoles { None = 0, Learner = 1, Teacher = 2, Manager = 4 }
[Flags]
public enum PersonalClassDelegatedCapabilities { None = 0, PublishCommunication = 1, ManageCommunication = 2 }

public sealed class PersonalClassParticipant
{
    private PersonalClassParticipant() { }
    public Guid Id { get; private set; }
    public Guid PersonalClassId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public Guid? InvitationId { get; private set; }
    public PersonalClassLocalRoles LocalRoles { get; private set; } = PersonalClassLocalRoles.Learner;
    public PersonalClassDelegatedCapabilities DelegatedCapabilities { get; private set; }
    public string? EndReason { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static PersonalClassParticipant Join(PersonalClassInvitation invitation, DateTime utcNow)
    {
        if (!invitation.AcceptedAtUtc.HasValue || utcNow.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("پذیرش صریح دعوت لازم است.");
        return new PersonalClassParticipant { Id = Guid.NewGuid(), PersonalClassId = invitation.PersonalClassId,
            StudentUserId = invitation.DestinationUserId, InvitationId = invitation.Id, JoinedAtUtc = utcNow,
            LocalRoles = invitation.LocalRoles, DelegatedCapabilities = invitation.DelegatedCapabilities };
    }
    public static PersonalClassParticipant Admit(Guid classId, Guid userId, PersonalClassLocalRoles roles, DateTime utcNow)
    {
        if (classId == Guid.Empty || userId == Guid.Empty || utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("عضویت مستقل معتبر لازم است.");
        var member = new PersonalClassParticipant { Id = Guid.NewGuid(), PersonalClassId = classId, StudentUserId = userId, JoinedAtUtc = utcNow };
        member.ChangeRoles(roles, PersonalClassDelegatedCapabilities.None);
        return member;
    }

    public void ChangeRoles(PersonalClassLocalRoles roles, PersonalClassDelegatedCapabilities capabilities)
    {
        if (EndedAtUtc.HasValue) throw new InvalidOperationException("عضویت پایان‌یافته قابل تغییر نیست.");
        ValidateRoles(roles, capabilities);
        LocalRoles = roles;
        DelegatedCapabilities = capabilities;
    }

    internal static void ValidateRoles(PersonalClassLocalRoles roles, PersonalClassDelegatedCapabilities capabilities)
    {
        if (roles == PersonalClassLocalRoles.None || (roles & ~(PersonalClassLocalRoles.Learner | PersonalClassLocalRoles.Teacher | PersonalClassLocalRoles.Manager)) != 0 ||
            (capabilities & ~(PersonalClassDelegatedCapabilities.PublishCommunication | PersonalClassDelegatedCapabilities.ManageCommunication)) != 0 ||
            capabilities != PersonalClassDelegatedCapabilities.None && !roles.HasFlag(PersonalClassLocalRoles.Teacher))
            throw new InvalidOperationException("نقش و تفویض صریح معتبر لازم است.");
    }

    public void End(DateTime utcNow, string? reason = null)
    {
        if (utcNow.Kind != DateTimeKind.Utc || utcNow < JoinedAtUtc) throw new ArgumentException("زمان معتبر نیست.");
        if (reason?.Trim().Length > 500) throw new ArgumentException("دلیل طولانی است.");
        if (EndedAtUtc.HasValue) return;
        EndedAtUtc = utcNow;
        EndReason = reason?.Trim();
    }
}
