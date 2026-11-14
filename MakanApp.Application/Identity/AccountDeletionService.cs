using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Identity;

public sealed record RequestAccountDeletionCommand(Guid ClientOperationId);
public sealed record AccountDeletionStatusResult(Guid Id, DateTime RequestedAtUtc, string Status, bool ExecutionEnabled, DateTime? ScheduledExecutionAtUtc)
{
    public string PolicyVersion { get; init; } = "";
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public int AttemptCount { get; init; }
    public string? SafeFailureCode { get; init; }
    public string? FailureStep { get; init; }
    public string Version { get; init; } = "";
}
public sealed record AccountDeletionQueueItem(Guid UserId, AccountDeletionStatusResult Request);
public sealed record AccountDeletionPage(IReadOnlyCollection<AccountDeletionQueueItem> Items, int Total, int Page, int PageSize);
public sealed record AccountDeletionStepResult(string Step, string Status, int AttemptCount, DateTime? StartedAtUtc, DateTime? CompletedAtUtc, string? SafeFailureCode);
public sealed record AccountDeletionDetail(AccountDeletionQueueItem Item, IReadOnlyCollection<AccountDeletionStepResult> Steps);
public sealed record RetryAccountDeletionCommand(Guid ClientOperationId, string ExpectedVersion);
public interface IAccountDeletionStore
{
    Task<AccountDeletionStatusResult> RequestAsync(Guid userId, Guid sessionId, Guid operation, string correlationId, CancellationToken ct);
    Task<AccountDeletionStatusResult?> StatusAsync(Guid userId, CancellationToken ct);
    Task<AccountDeletionPage> PageAsync(int page, int pageSize, CancellationToken ct);
    Task<AccountDeletionDetail?> DetailAsync(Guid requestId, CancellationToken ct);
    Task<AccountDeletionStatusResult> RetryAsync(Guid userId, Guid sessionId, Guid requestId, RetryAccountDeletionCommand command, CancellationToken ct);
}
public sealed class AccountDeletionService(IAccountDeletionStore store, CommercialActorAccess actors, IPlatformPermissionResolver permissions)
{
    public async Task<AccountDeletionPage> PageAsync(Guid user, Guid session, int page, int pageSize, CancellationToken ct)
    {
        await actors.EnsureAsync(user, session, ct);
        if (!await permissions.HasPermissionAsync(user, PlatformPermissionKeys.PrivacyRequestsRead, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری حریم خصوصی لازم است.");
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100) throw new PlatformException(PlatformErrorCodes.QueryInvalid, "صفحه‌بندی معتبر لازم است.");
        return await store.PageAsync(page, pageSize, ct);
    }
    public async Task<AccountDeletionStatusResult> RequestAsync(Guid user, Guid session, RequestAccountDeletionCommand command, string correlation, CancellationToken ct)
    {
        await actors.EnsureAsync(user, session, ct);
        if (command.ClientOperationId == Guid.Empty) throw new IdentityException(IdentityErrorCodes.ProfileValidationFailed, "شناسهٔ عملیات لازم است.");
        return await store.RequestAsync(user, session, command.ClientOperationId, correlation, ct);
    }
    public async Task<AccountDeletionStatusResult?> StatusAsync(Guid user, Guid session, CancellationToken ct)
    { await actors.EnsureAsync(user, session, ct); return await store.StatusAsync(user, ct); }

    public async Task<AccountDeletionDetail> DetailAsync(Guid user, Guid session, Guid requestId, CancellationToken ct)
    {
        await EnsurePermissionAsync(user, session, PlatformPermissionKeys.PrivacyRequestsRead, ct);
        return await store.DetailAsync(requestId, ct) ?? throw new IdentityException(AccountDeletionErrorCodes.NotFound, "درخواست قابل دسترس نیست.");
    }
    public async Task<AccountDeletionStatusResult> RetryAsync(Guid user, Guid session, Guid requestId, RetryAccountDeletionCommand command, CancellationToken ct)
    {
        await EnsurePermissionAsync(user, session, PlatformPermissionKeys.PrivacyRequestsManage, ct);
        if (command.ClientOperationId == Guid.Empty || string.IsNullOrWhiteSpace(command.ExpectedVersion) || command.ExpectedVersion.Length > 32)
            throw new IdentityException(IdentityErrorCodes.ProfileValidationFailed, "شناسهٔ عملیات و نسخهٔ معتبر لازم است.");
        return await store.RetryAsync(user, session, requestId, command, ct);
    }
    private async Task EnsurePermissionAsync(Guid user, Guid session, string permission, CancellationToken ct)
    {
        await actors.EnsureAsync(user, session, ct);
        if (!await permissions.HasPermissionAsync(user, permission, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری حریم خصوصی لازم است.");
    }
}
