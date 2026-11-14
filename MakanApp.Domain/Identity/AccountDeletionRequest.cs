namespace MakanApp.Domain.Identity;

public enum AccountDeletionStatus { Requested = 1, Scheduled = 2, Processing = 3, NeedsAttention = 4, Completed = 5 }

// رسید درخواست حفظ می‌شود؛ پیشرفت، claim و سیاست اجرا مستقل از نشست فعالِ درخواست‌کننده‌اند.
public sealed class AccountDeletionRequest
{
    public const string CurrentPolicyVersion = "account-deletion-v1";
    private AccountDeletionRequest() { }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RequestedBySessionId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public DateTime RequestedAtUtc { get; private set; }
    public string PolicyStatus { get; private set; } = "EXECUTION_DISABLED";
    public string PolicyVersion { get; private set; } = CurrentPolicyVersion;
    public AccountDeletionStatus Status { get; private set; } = AccountDeletionStatus.Requested;
    public DateTime? ScheduledExecutionAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? LastAttemptAtUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public Guid? ClaimToken { get; private set; }
    public DateTime? ClaimedUntilUtc { get; private set; }
    public string? FailureCode { get; private set; }
    public AccountDeletionStepKind? FailureStep { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public static AccountDeletionRequest Create(Guid user, Guid session, Guid operation, DateTime now)
    {
        if (user == Guid.Empty || session == Guid.Empty || operation == Guid.Empty || now.Kind != DateTimeKind.Utc) throw new ArgumentException("درخواست حذف معتبر نیست.");
        return new() { Id = Guid.NewGuid(), UserId = user, RequestedBySessionId = session, ClientOperationId = operation, RequestedAtUtc = now };
    }

    public void Schedule(DateTime scheduledAtUtc)
    {
        EnsureUtc(scheduledAtUtc);
        if (Status != AccountDeletionStatus.Requested || scheduledAtUtc < RequestedAtUtc)
            throw new InvalidOperationException("درخواست قابل زمان‌بندی نیست.");
        ScheduledExecutionAtUtc = scheduledAtUtc; Status = AccountDeletionStatus.Scheduled;
        PolicyStatus = "EXECUTION_SCHEDULED"; PolicyVersion = CurrentPolicyVersion;
    }

    public void Claim(Guid token, DateTime now, TimeSpan lease)
    {
        EnsureUtc(now);
        if (token == Guid.Empty || lease <= TimeSpan.Zero || Status is not (AccountDeletionStatus.Scheduled or AccountDeletionStatus.Processing) ||
            !ScheduledExecutionAtUtc.HasValue || ScheduledExecutionAtUtc > now || NextAttemptAtUtc > now || ClaimedUntilUtc > now)
            throw new InvalidOperationException("درخواست قابل claim نیست.");
        if (ClaimToken.HasValue) ConsecutiveFailures = checked(ConsecutiveFailures + 1);
        ClaimToken = token; ClaimedUntilUtc = now + lease; LastAttemptAtUtc = now; NextAttemptAtUtc = null;
        AttemptCount = checked(AttemptCount + 1); Status = AccountDeletionStatus.Processing;
    }

    public void EnsureClaim(Guid token, DateTime now)
    {
        EnsureUtc(now);
        if (Status != AccountDeletionStatus.Processing || token == Guid.Empty || ClaimToken != token || !ClaimedUntilUtc.HasValue || ClaimedUntilUtc <= now)
            throw new AccountDeletionClaimLostException();
    }

    public void MarkStarted(Guid token, DateTime now)
    { EnsureClaim(token, now); StartedAtUtc ??= now; }

    public void ReleaseProgress(Guid token, DateTime now)
    {
        EnsureClaim(token, now); ConsecutiveFailures = 0; FailureCode = null; FailureStep = null;
        ClaimToken = null; ClaimedUntilUtc = null; NextAttemptAtUtc = null;
    }

    public void Fail(Guid token, AccountDeletionStepKind step, string code, bool retryable, DateTime now, int maximumFailures, bool failureAlreadyCounted = false)
    {
        EnsureClaim(token, now);
        if (!Enum.IsDefined(step) || string.IsNullOrWhiteSpace(code) || code.Length > 100 || maximumFailures < 1)
            throw new ArgumentException("وضعیت شکست معتبر نیست.");
        if (failureAlreadyCounted && ConsecutiveFailures < maximumFailures) throw new InvalidOperationException("بودجهٔ بازیابی هنوز تمام نشده است.");
        // انقضای claim در Claim شمرده شده؛ تعمیر نهایی همان شکست را دوباره نمی‌شمارد.
        if (!failureAlreadyCounted) ConsecutiveFailures = checked(ConsecutiveFailures + 1);
        FailureCode = code; FailureStep = step;
        var retry = retryable && ConsecutiveFailures < maximumFailures;
        Status = retry ? AccountDeletionStatus.Processing : AccountDeletionStatus.NeedsAttention;
        NextAttemptAtUtc = retry ? now.AddSeconds(Math.Min(300, Math.Pow(2, ConsecutiveFailures))) : null;
        ClaimToken = null; ClaimedUntilUtc = null;
    }

    public void Resume(DateTime now)
    {
        EnsureUtc(now);
        if (Status != AccountDeletionStatus.NeedsAttention) throw new InvalidOperationException("درخواست نیازمند ادامهٔ دستی نیست.");
        Status = AccountDeletionStatus.Scheduled; ConsecutiveFailures = 0; NextAttemptAtUtc = null;
        ClaimToken = null; ClaimedUntilUtc = null;
    }

    public void Complete(Guid token, DateTime now, IReadOnlyCollection<AccountDeletionStep> steps)
    {
        EnsureClaim(token, now);
        if (!StartedAtUtc.HasValue || steps.Count != Enum.GetValues<AccountDeletionStepKind>().Length ||
            steps.Any(s => s.RequestId != Id || s.Status != AccountDeletionStepStatus.Completed) ||
            steps.Select(s => s.Step).Distinct().Count() != steps.Count)
            throw new InvalidOperationException("تمام مراحل حذف باید به‌طور پایدار تکمیل شده باشند.");
        Status = AccountDeletionStatus.Completed; CompletedAtUtc = now; PolicyStatus = "EXECUTION_COMPLETED";
        FailureCode = null; FailureStep = null; ClaimToken = null; ClaimedUntilUtc = null; NextAttemptAtUtc = null;
    }

    private static void EnsureUtc(DateTime value)
    { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("زمان باید UTC باشد."); }
}

public sealed class AccountDeletionClaimLostException() : Exception("مالکیت اجرای حذف معتبر نیست.");
