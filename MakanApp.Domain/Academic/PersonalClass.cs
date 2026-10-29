namespace MakanApp.Domain.Academic;

public sealed class PersonalClass
{
    private PersonalClass() { }
    public Guid Id { get; private set; }
    public Guid HostUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public Guid ClientOperationId { get; private set; }
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
    }
}
