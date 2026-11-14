using MakanApp.Domain.Identity;

namespace MakanApp.Application.Identity;

public sealed class AccountDeletionOptions
{
    public const string SectionName = "AccountDeletion";
    public bool ExecutionEnabled { get; init; }
    public int? GracePeriodHours { get; init; }

    public bool IsValid()
    {
        if (!ExecutionEnabled) return GracePeriodHours is null or >= 0;
        if (GracePeriodHours is null or < 0) return false;
        try { _ = DateTime.UtcNow.AddHours(GracePeriodHours.Value); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
    public DateTime ScheduleAt(DateTime now)
    {
        if (!ExecutionEnabled || !GracePeriodHours.HasValue || !IsValid() || now.Kind != DateTimeKind.Utc)
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.ExecutionDisabled);
        return now.AddHours(GracePeriodHours.Value);
    }
}

public static class AccountDeletionErrorCodes
{
    public const string NotFound = "ACCOUNT_DELETION_NOT_FOUND";
    public const string Conflict = "ACCOUNT_DELETION_CONFLICT";
    public const string ExecutionDisabled = "ACCOUNT_DELETION_EXECUTION_DISABLED";
    public const string LastOrganizationManager = "ACCOUNT_DELETION_LAST_ORGANIZATION_MANAGER";
    public const string LastPersonalClassManager = "ACCOUNT_DELETION_LAST_PERSONAL_CLASS_MANAGER";
    public const string LastPlatformOwner = "ACCOUNT_DELETION_LAST_PLATFORM_OWNER";
    public const string SchoolManagerDependency = "ACCOUNT_DELETION_SCHOOL_MANAGER_DEPENDENCY";
    public const string ConversationOwnership = "ACCOUNT_DELETION_CONVERSATION_OWNERSHIP";
    public const string KnowledgeUnavailable = "ACCOUNT_DELETION_KNOWLEDGE_UNAVAILABLE";
    public const string OwnershipConflict = "ACCOUNT_DELETION_SOURCE_OWNERSHIP_CONFLICT";
    public const string ReferentialConflict = "ACCOUNT_DELETION_REFERENTIAL_CONFLICT";
    public const string InvariantFailed = "ACCOUNT_DELETION_INVARIANT_FAILED";
    public const string StepFailed = "ACCOUNT_DELETION_STEP_FAILED";
    public const string RecoveryExhausted = "ACCOUNT_DELETION_RECOVERY_EXHAUSTED";
}

public sealed class AccountDeletionExecutionException(string code, bool retryable = false)
    : Exception("اجرای حذف حساب در این مرحله قابل تکمیل نیست.")
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
}

public sealed record AccountDeletionExecutionContext(Guid RequestId, Guid UserId, Guid RequestedBySessionId,
    Guid ClaimToken, string PolicyVersion)
{
    public string CorrelationId => "account-deletion-" + RequestId.ToString("N");
}

// این قراردادها داخلی‌اند؛ فقط Worker دارای claim معتبر آن‌ها را از پیشرفت پایدار فراخوانی می‌کند.
public interface IIdentityAccountDeletionLifecycle
{
    Task FreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task AnonymizeAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface IOrganizationAccountDeletionLifecycle
{
    Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface IPersonalClassAccountDeletionLifecycle
{
    Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface IPlatformAccountDeletionLifecycle
{
    Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface IGuardianAccountDeletionLifecycle
{
    Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface INotificationAccountDeletionLifecycle
{
    Task RevokeAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task PurgePreferencesAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface IMessagingAccountDeletionLifecycle
{
    Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task PurgePrivateStateAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface IAiAccountDeletionLifecycle
{
    Task PurgePrivateContextAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public interface ICommerceAccountDeletionLifecycle
{
    Task CloseAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}
public sealed record AccountDeletionKnowledgeWork(AccountDeletionExecutionContext Context, Guid SourceId, int Version, string CommandRequestId);
public interface IAccountDeletionKnowledgePurger
{
    Task RevokeSourcesAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task<AccountDeletionKnowledgeWork?> PrepareNextAsync(AccountDeletionExecutionContext context, CancellationToken ct);
    Task<string> DeleteAsync(AccountDeletionKnowledgeWork work, CancellationToken ct);
    Task CompleteAsync(AccountDeletionKnowledgeWork work, string outcome, CancellationToken ct);
    Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct);
}

public sealed class AccountDeletionWorkflow(IIdentityAccountDeletionLifecycle identity, IOrganizationAccountDeletionLifecycle organizations,
    IPersonalClassAccountDeletionLifecycle classes, IPlatformAccountDeletionLifecycle platform, IGuardianAccountDeletionLifecycle guardians,
    INotificationAccountDeletionLifecycle notifications, IMessagingAccountDeletionLifecycle messaging, IAiAccountDeletionLifecycle ai,
    ICommerceAccountDeletionLifecycle commerce, IAccountDeletionKnowledgePurger knowledge)
{
    public async Task ExecuteSqlStepAsync(AccountDeletionStepKind step, AccountDeletionExecutionContext context, CancellationToken ct)
    {
        switch (step)
        {
            case AccountDeletionStepKind.FreezeAccount:
                // مانع مدیر/مالک پیش از مسدودشدن برگشت‌ناپذیر حساب بررسی می‌شود؛ همه در همان transaction کوتاه SQL هستند.
                await platform.EnsureCanFreezeAsync(context, ct);
                await organizations.EnsureCanFreezeAsync(context, ct);
                await classes.EnsureCanFreezeAsync(context, ct);
                await messaging.EnsureCanFreezeAsync(context, ct);
                await identity.FreezeAsync(context, ct);
                await notifications.RevokeAsync(context, ct);
                break;
            case AccountDeletionStepKind.EndAccessRelationships:
                await organizations.EndAsync(context, ct); await classes.EndAsync(context, ct);
                await platform.EndAsync(context, ct); await guardians.EndAsync(context, ct);
                break;
            case AccountDeletionStepKind.PurgePrivateAddressBook:
                await messaging.PurgePrivateStateAsync(context, ct);
                await notifications.PurgePreferencesAsync(context, ct); await ai.PurgePrivateContextAsync(context, ct);
                break;
            case AccountDeletionStepKind.RevokePersonalSources:
                await knowledge.RevokeSourcesAsync(context, ct); break;
            case AccountDeletionStepKind.AnonymizeIdentity:
                await identity.AnonymizeAsync(context, ct); break;
            case AccountDeletionStepKind.ClosePersonalEntitlements:
                await commerce.CloseAsync(context, ct); break;
            case AccountDeletionStepKind.Finalize:
                await identity.VerifyAsync(context, ct); await organizations.VerifyAsync(context, ct);
                await classes.VerifyAsync(context, ct); await platform.VerifyAsync(context, ct);
                await guardians.VerifyAsync(context, ct); await notifications.VerifyAsync(context, ct);
                await messaging.VerifyAsync(context, ct); await ai.VerifyAsync(context, ct);
                await commerce.VerifyAsync(context, ct); await knowledge.VerifyAsync(context, ct);
                break;
            default: throw new InvalidOperationException("مرحلهٔ SQL معتبر نیست.");
        }
    }
}
