namespace MakanApp.Domain.Identity;

public enum AccountDeletionStepKind
{
    FreezeAccount = 1, EndAccessRelationships = 2, PurgePrivateAddressBook = 3, RevokePersonalSources = 4,
    DeletePersonalKnowledge = 5, AnonymizeIdentity = 6, ClosePersonalEntitlements = 7, Finalize = 8
}
public enum AccountDeletionStepStatus { Pending = 1, Processing = 2, Completed = 3, Failed = 4 }

public sealed class AccountDeletionStep
{
    private AccountDeletionStep() { }
    public Guid RequestId { get; private set; }
    public AccountDeletionStepKind Step { get; private set; }
    public AccountDeletionStepStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AccountDeletionStep Create(Guid request, AccountDeletionStepKind step)
    {
        if (request == Guid.Empty || !Enum.IsDefined(step)) throw new ArgumentException("مرحلهٔ حذف معتبر نیست.");
        return new() { RequestId = request, Step = step, Status = AccountDeletionStepStatus.Pending };
    }
    public void Start(DateTime now)
    {
        if (Status == AccountDeletionStepStatus.Completed || now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("مرحله قابل شروع نیست.");
        Status = AccountDeletionStepStatus.Processing; AttemptCount = checked(AttemptCount + 1);
        StartedAtUtc ??= now; FailureCode = null;
    }
    public void Complete(DateTime now)
    {
        if (Status != AccountDeletionStepStatus.Processing || now.Kind != DateTimeKind.Utc || now < StartedAtUtc)
            throw new InvalidOperationException("مرحله قابل تکمیل نیست.");
        Status = AccountDeletionStepStatus.Completed; CompletedAtUtc = now; FailureCode = null;
    }
    public void Fail(string code)
    {
        if (Status == AccountDeletionStepStatus.Completed || string.IsNullOrWhiteSpace(code) || code.Length > 100)
            throw new InvalidOperationException("شکست مرحله معتبر نیست.");
        Status = AccountDeletionStepStatus.Failed; FailureCode = code;
    }
}
