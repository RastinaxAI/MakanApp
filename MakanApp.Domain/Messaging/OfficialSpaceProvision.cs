namespace MakanApp.Domain.Messaging;

public enum OfficialSpaceState { Pending = 1, Ready = 2, RepairRequired = 3 }
public enum OfficialSourceKind { Organization = 1, Class = 2 }

public sealed class OfficialSpaceProvision
{
    private OfficialSpaceProvision() { }
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid? ClassId { get; private set; }
    public ConversationType Purpose { get; private set; }
    public OfficialSpaceState State { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public string? LastFailureCategory { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static OfficialSpaceProvision Request(Guid organizationId, Guid? classId, ConversationType purpose, DateTime now)
    {
        if (organizationId == Guid.Empty || classId == Guid.Empty || purpose is not ConversationType.Group and not ConversationType.Channel || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("منبع، کاربرد یا زمان فضای رسمی معتبر نیست.");
        return new() { Id = Guid.NewGuid(), OrganizationId = organizationId, ClassId = classId, Purpose = purpose,
            State = OfficialSpaceState.Pending, CreatedAtUtc = now };
    }

    public void RecordAttempt(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد.");
        AttemptCount = checked(AttemptCount + 1); LastAttemptAtUtc = now;
    }
    public void Ready() { State = OfficialSpaceState.Ready; LastFailureCategory = null; }
    public void RequireRepair(string category)
    {
        if (category is not ("BindingConflict" or "SourceUnavailable" or "ConversationUnavailable"))
            throw new ArgumentException("دستهٔ شکست قابل ثبت نیست.");
        State = OfficialSpaceState.RepairRequired; LastFailureCategory = category;
    }
}
