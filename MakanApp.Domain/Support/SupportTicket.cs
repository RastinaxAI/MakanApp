namespace MakanApp.Domain.Support;

public enum SupportTicketStatus { Open = 1, InProgress = 2, WaitingForUser = 3, WaitingForSupport = 4, Resolved = 5 }

public sealed class SupportTicket
{
    private SupportTicket() { }
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid? AssignedStaffUserId { get; private set; }
    public string Subject { get; private set; } = "";
    public string Category { get; private set; } = "";
    public SupportTicketStatus Status { get; private set; }
    public long Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static SupportTicket Create(Guid owner, string subject, string category, DateTime now)
    {
        if (owner == Guid.Empty || string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(category) || category.Trim().Length > 64 || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("مشخصات تیکت معتبر نیست.");
        return new() { Id = Guid.NewGuid(), OwnerUserId = owner, Subject = subject.Trim(), Category = category.Trim(),
            Status = SupportTicketStatus.Open, Version = 1, CreatedAtUtc = now, UpdatedAtUtc = now };
    }
    public void Reply(bool staff, DateTime now)
    {
        EnsureOpen(now); Status = staff ? SupportTicketStatus.WaitingForUser : SupportTicketStatus.WaitingForSupport;
        Version++; UpdatedAtUtc = now;
    }
    public void Assign(Guid staff, DateTime now)
    {
        EnsureOpen(now); if (staff == Guid.Empty) throw new ArgumentException("کارشناس معتبر لازم است.");
        AssignedStaffUserId = staff; Version++; UpdatedAtUtc = now;
        if (Status == SupportTicketStatus.Open) Status = SupportTicketStatus.InProgress;
    }
    public void Resolve(DateTime now)
    {
        EnsureOpen(now); Status = SupportTicketStatus.Resolved; ResolvedAtUtc = now; UpdatedAtUtc = now; Version++;
    }
    private void EnsureOpen(DateTime now)
    {
        if (now.Kind != DateTimeKind.Utc || Status == SupportTicketStatus.Resolved)
            throw new InvalidOperationException("تیکت نهایی قابل تغییر نیست؛ بازگشایی به سیاست مصوب نیاز دارد.");
    }
}

public sealed class SupportTicketMessage
{
    private SupportTicketMessage() { }
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public bool IsStaff { get; private set; }
    public string Body { get; private set; } = "";
    public long TicketVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static SupportTicketMessage Create(Guid ticket, Guid author, bool staff, string body, long version, DateTime now)
    {
        if (ticket == Guid.Empty || author == Guid.Empty || string.IsNullOrWhiteSpace(body) || body.Trim().Length > 8000 || version < 1 || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("پیام تیکت معتبر نیست.");
        return new() { Id = Guid.NewGuid(), TicketId = ticket, AuthorUserId = author, IsStaff = staff, Body = body.Trim(), TicketVersion = version, CreatedAtUtc = now };
    }
}

public sealed class SupportTicketAttachment
{
    private SupportTicketAttachment() { }
    public Guid TicketId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public static SupportTicketAttachment Create(Guid ticket, Guid message, Guid file) => new() { TicketId = ticket, MessageId = message, FileAssetId = file };
}

public sealed class SupportTicketOperation
{
    private SupportTicketOperation() { }
    public Guid ActorUserId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public Guid TicketId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public long ResultVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public static SupportTicketOperation Create(Guid actor, Guid operation, Guid ticket, string hash, long version, DateTime now) =>
        new() { ActorUserId = actor, ClientOperationId = operation, TicketId = ticket, Fingerprint = hash, ResultVersion = version, CreatedAtUtc = now };
}
