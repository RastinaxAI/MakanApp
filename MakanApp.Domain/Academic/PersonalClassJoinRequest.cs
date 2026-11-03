namespace MakanApp.Domain.Academic;

public enum PersonalClassJoinRequestStatus { Pending = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

public sealed class PersonalClassJoinRequest
{
    private PersonalClassJoinRequest() { }
    public Guid Id { get; private set; }
    public Guid PersonalClassId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public PersonalClassJoinRequestStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public string? ClosureReason { get; private set; }
    public Guid? MembershipId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PersonalClassJoinRequest Create(Guid classId, Guid userId, Guid operationId, DateTime nowUtc)
    {
        if (classId == Guid.Empty || userId == Guid.Empty || operationId == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("درخواست عضویت معتبر لازم است.");
        return new() { Id = Guid.NewGuid(), PersonalClassId = classId, UserId = userId, ClientOperationId = operationId,
            Status = PersonalClassJoinRequestStatus.Pending, CreatedAtUtc = nowUtc };
    }

    public void Close(PersonalClassJoinRequestStatus status, string reason, DateTime nowUtc, Guid? membershipId = null)
    {
        if (Status != PersonalClassJoinRequestStatus.Pending || status == PersonalClassJoinRequestStatus.Pending || !Enum.IsDefined(status))
            throw new InvalidOperationException("درخواست عضویت باز لازم است.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500 || nowUtc.Kind != DateTimeKind.Utc || nowUtc < CreatedAtUtc ||
            membershipId == Guid.Empty || (status == PersonalClassJoinRequestStatus.Approved) != membershipId.HasValue)
            throw new ArgumentException("نتیجهٔ درخواست عضویت معتبر نیست.");
        Status = status; ClosureReason = reason.Trim(); ClosedAtUtc = nowUtc; MembershipId = membershipId;
    }
}
