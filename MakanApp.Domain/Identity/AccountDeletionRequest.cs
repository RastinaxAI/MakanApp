namespace MakanApp.Domain.Identity;

// اجرای حذف تا تصویب policy جداگانهٔ هر کلاس داده مجاز نیست؛ این مدل صرفاً رسید درخواست است.
public sealed class AccountDeletionRequest
{
    private AccountDeletionRequest() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RequestedBySessionId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public string PolicyStatus { get; private set; } = "PRODUCT_LEGAL_DECISION_REQUIRED";
    public byte[] RowVersion { get; private set; } = [];
    public static AccountDeletionRequest Create(Guid user, Guid session, Guid operation, DateTime now)
    {
        if (user == Guid.Empty || session == Guid.Empty || operation == Guid.Empty || now.Kind != DateTimeKind.Utc) throw new ArgumentException("درخواست حذف معتبر نیست.");
        return new() { Id = Guid.NewGuid(), UserId = user, RequestedBySessionId = session, ClientOperationId = operation, RequestedAtUtc = now };
    }
}
