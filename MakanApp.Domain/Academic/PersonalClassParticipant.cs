namespace MakanApp.Domain.Academic;

public sealed class PersonalClassParticipant
{
    private PersonalClassParticipant() { }
    public Guid Id { get; private set; }
    public Guid PersonalClassId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public Guid InvitationId { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static PersonalClassParticipant Join(PersonalClassInvitation invitation, DateTime utcNow)
    {
        if (!invitation.AcceptedAtUtc.HasValue || utcNow.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("پذیرش صریح دعوت لازم است.");
        return new PersonalClassParticipant { Id = Guid.NewGuid(), PersonalClassId = invitation.PersonalClassId,
            StudentUserId = invitation.DestinationUserId, InvitationId = invitation.Id, JoinedAtUtc = utcNow };
    }
    public void End(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc || utcNow < JoinedAtUtc) throw new ArgumentException("زمان معتبر نیست.");
        EndedAtUtc ??= utcNow;
    }
}
