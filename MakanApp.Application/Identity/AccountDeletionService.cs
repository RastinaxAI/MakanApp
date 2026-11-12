using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;

namespace MakanApp.Application.Identity;

public sealed record RequestAccountDeletionCommand(Guid ClientOperationId);
public sealed record AccountDeletionStatusResult(Guid Id, DateTime RequestedAtUtc, string Status, bool ExecutionEnabled, DateTime? ScheduledExecutionAtUtc);
public sealed record AccountDeletionQueueItem(Guid UserId, AccountDeletionStatusResult Request);
public sealed record AccountDeletionPage(IReadOnlyCollection<AccountDeletionQueueItem> Items, int Total, int Page, int PageSize);
public interface IAccountDeletionStore
{
    Task<AccountDeletionStatusResult> RequestAsync(Guid userId, Guid sessionId, Guid operation, string correlationId, CancellationToken ct);
    Task<AccountDeletionStatusResult?> StatusAsync(Guid userId, CancellationToken ct);
    Task<AccountDeletionPage> PageAsync(int page, int pageSize, CancellationToken ct);
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
}
