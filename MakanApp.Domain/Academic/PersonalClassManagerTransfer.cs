namespace MakanApp.Domain.Academic;

public enum PersonalClassManagerTransferStatus { Pending = 1, Accepted = 2, Declined = 3, Cancelled = 4 }

public sealed class PersonalClassManagerTransfer
{
    private PersonalClassManagerTransfer() { }
    public Guid Id { get; private set; }
    public Guid PersonalClassId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? PreviousManagerUserId { get; private set; }
    public Guid TargetUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public PersonalClassManagerTransferStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PersonalClassManagerTransfer Create(Guid classId, Guid actor, Guid? previousManager, Guid target,
        Guid operationId, string reason, DateTime nowUtc)
    {
        if (classId == Guid.Empty || actor == Guid.Empty || previousManager == Guid.Empty || target == Guid.Empty ||
            target == previousManager || operationId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("پیشنهاد انتقال مسئولیت معتبر نیست.");
        return new() { Id = Guid.NewGuid(), PersonalClassId = classId, RequestedByUserId = actor,
            PreviousManagerUserId = previousManager, TargetUserId = target, ClientOperationId = operationId,
            Reason = reason.Trim(), Status = PersonalClassManagerTransferStatus.Pending, CreatedAtUtc = nowUtc };
    }

    public void Decide(PersonalClassManagerTransferStatus decision, Guid actor, DateTime nowUtc)
    {
        if (Status != PersonalClassManagerTransferStatus.Pending || decision == PersonalClassManagerTransferStatus.Pending || !Enum.IsDefined(decision))
            throw new InvalidOperationException("انتقال باز لازم است.");
        if ((decision == PersonalClassManagerTransferStatus.Cancelled ? actor != RequestedByUserId : actor != TargetUserId) ||
            nowUtc.Kind != DateTimeKind.Utc || nowUtc < CreatedAtUtc)
            throw new InvalidOperationException("تأیید مسئولیت فقط توسط مخاطب معتبر انجام می‌شود.");
        Status = decision; DecidedAtUtc = nowUtc;
    }
}
