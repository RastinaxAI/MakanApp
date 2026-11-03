namespace MakanApp.Domain.Academic;

public enum PersonalClassState { Provisioning = 1, ReadyForSetup = 2, Active = 3, ProvisioningFailed = 4, Suspended = 5, Archived = 6 }
public enum PersonalClassMigrationState { Reviewed = 0, LegacyActive = 1, LegacyArchived = 2, NeedsManagerMapping = 3, NeedsSpaceProvisioning = 4, Conflict = 5 }

public sealed class PersonalClass
{
    private PersonalClass() { }
    public Guid Id { get; private set; }
    public Guid HostUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid ClientOperationId { get; private set; }
    public Guid? FormationRequestId { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public int? Capacity { get; private set; }
    public bool AcceptingJoinRequests { get; private set; }
    public PersonalClassState State { get; private set; } = PersonalClassState.Active;
    public PersonalClassMigrationState MigrationState { get; private set; } = PersonalClassMigrationState.LegacyActive;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static PersonalClass Create(Guid hostUserId, string title, Guid operationId, DateTime utcNow)
    {
        var normalized = title?.Trim();
        if (hostUserId == Guid.Empty || operationId == Guid.Empty || string.IsNullOrWhiteSpace(normalized) || normalized.Length > 200
            || utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("کلاس مستقل معتبر نیست.");
        return new PersonalClass { Id = Guid.NewGuid(), HostUserId = hostUserId, Title = normalized,
            ClientOperationId = operationId, CreatedAtUtc = utcNow };
    }

    public void Archive(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc || utcNow < CreatedAtUtc) throw new ArgumentException("زمان معتبر نیست.");
        ArchivedAtUtc ??= utcNow;
        State = PersonalClassState.Archived;
        AcceptingJoinRequests = false;
    }

    public static PersonalClass FromApproval(PersonalClassRequest request, DateTime utcNow)
    {
        if (request.Status is not (PersonalClassRequestStatus.Submitted or PersonalClassRequestStatus.UnderReview))
            throw new InvalidOperationException("درخواست بررسی‌پذیر لازم است.");
        var entity = Create(request.RequesterUserId, request.Title, request.ClientOperationId, utcNow);
        entity.FormationRequestId = request.Id;
        entity.Capacity = request.SuggestedCapacity;
        entity.State = PersonalClassState.Provisioning;
        entity.MigrationState = PersonalClassMigrationState.Reviewed;
        return entity;
    }

    public void SetManager(Guid managerUserId)
    {
        if (managerUserId == Guid.Empty || State == PersonalClassState.Archived)
            throw new InvalidOperationException("مدیر مسئول معتبر لازم است.");
        ManagerUserId = managerUserId;
    }

    public void Configure(int capacity, bool acceptingJoinRequests, int activeLearners)
    {
        if (State == PersonalClassState.Archived || capacity <= 0 || capacity < activeLearners)
            throw new InvalidOperationException("ظرفیت نمی‌تواند از تعداد فراگیر فعال کمتر باشد.");
        Capacity = capacity;
        AcceptingJoinRequests = acceptingJoinRequests;
    }

    public void RecordProvisioning(bool ready)
    {
        if (State is PersonalClassState.Archived or PersonalClassState.Suspended or PersonalClassState.Active) return;
        State = ready ? PersonalClassState.ReadyForSetup : PersonalClassState.ProvisioningFailed;
    }

    public void Activate(bool spacesReady)
    {
        if (State == PersonalClassState.Active) return;
        if (State is not (PersonalClassState.ReadyForSetup or PersonalClassState.Suspended) || !ManagerUserId.HasValue || !Capacity.HasValue || !spacesReady)
            throw new InvalidOperationException("مدیر، ظرفیت و هر دو فضای آماده برای فعال‌سازی لازم‌اند.");
        State = PersonalClassState.Active;
    }

    public void Suspend()
    {
        if (State == PersonalClassState.Suspended) return;
        if (State != PersonalClassState.Active) throw new InvalidOperationException("فقط کلاس فعال قابل تعلیق است.");
        State = PersonalClassState.Suspended;
    }
}
