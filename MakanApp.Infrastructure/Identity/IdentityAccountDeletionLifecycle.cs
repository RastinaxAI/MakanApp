using MakanApp.Application.Identity;
using MakanApp.Application.Storage;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Identity;

public sealed class IdentityAccountDeletionLifecycle(MakanDbContext db, TimeProvider clock,
    IFileAssetStore files, IFileAssetBoundAccessResolver bindings) : IIdentityAccountDeletionLifecycle
{
    public async Task FreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var user = await LockUserAsync(context.UserId, ct); var now = clock.GetUtcNow().UtcDateTime;
        user.BeginAccountDeletion(now);
        var sessions = await db.UserSessions.Where(s => s.UserId == user.Id && s.RevokedAtUtc == null).ToArrayAsync(ct);
        foreach (var session in sessions) session.Revoke(now);
        var tokens = await db.RefreshTokens.Where(t => t.RevokedAtUtc == null &&
            db.UserSessions.Any(s => s.Id == t.SessionId && s.UserId == user.Id)).ToArrayAsync(ct);
        foreach (var token in tokens) token.Revoke(now);
        var pending = await db.PhoneChangeRequests.Where(r => r.UserId == user.Id &&
            (r.State == PhoneChangeState.Preparing || r.State == PhoneChangeState.Ready)).ToArrayAsync(ct);
        foreach (var request in pending) request.Supersede();
    }

    public async Task AnonymizeAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var user = await LockUserAsync(context.UserId, ct); var now = clock.GetUtcNow().UtcDateTime;
        var photo = user.ProfilePhotoFileAssetId;
        var phoneChanges = await db.PhoneChangeRequests.Where(r => r.UserId == user.Id).ToArrayAsync(ct);
        foreach (var request in phoneChanges) request.DetachForAccountDeletion();
        if (user.PersonId.HasValue)
        {
            var person = await db.Persons.SingleAsync(p => p.Id == user.PersonId, ct);
            person.Anonymize(now);
        }
        user.Anonymize(now);
        // رفع FK تغییر شماره و unlink عکس باید قبل از حذف Credential و بررسی reference فایل در همان transaction ثبت شود.
        await db.SaveChangesAsync(ct);
        await db.UserCredentials.Where(c => c.UserId == user.Id && c.Kind == CredentialKind.MobilePhone).ExecuteDeleteAsync(ct);
        if (photo.HasValue)
        {
            var asset = await files.GetForUpdateAsync(photo.Value, ct);
            if (asset is not null && asset.UploadedByUserId == user.Id && asset.Status != FileAssetStatus.Deleted &&
                asset.RetainedAtUtc is null && !await bindings.IsBoundAsync(asset.Id, ct))
                asset.MarkDeleted(now);
        }
    }

    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == context.UserId, ct);
        if (user.AccountStatus != UserAccountStatus.Suspended || user.DeletionStartedAtUtc is null || user.AnonymizedAtUtc is null ||
            user.Bio is not null || user.ProfilePhotoFileAssetId is not null || user.Username?.StartsWith(User.DeletedUsernamePrefix, StringComparison.Ordinal) != true ||
            await db.UserCredentials.AnyAsync(c => c.UserId == user.Id && c.Kind == CredentialKind.MobilePhone, ct) ||
            await db.UserSessions.AnyAsync(s => s.UserId == user.Id && s.RevokedAtUtc == null, ct) ||
            await db.RefreshTokens.AnyAsync(t => t.RevokedAtUtc == null && db.UserSessions.Any(s => s.Id == t.SessionId && s.UserId == user.Id), ct) ||
            await db.PhoneChangeRequests.AnyAsync(r => r.UserId == user.Id && (r.CredentialId != null || r.State == PhoneChangeState.Preparing || r.State == PhoneChangeState.Ready), ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
        if (user.PersonId.HasValue && !await db.Persons.AnyAsync(p => p.Id == user.PersonId && p.DateOfBirth == null &&
                p.FirstName == Person.DeletedFirstName && p.LastName == Person.DeletedLastName && p.DisplayName == Person.DeletedDisplayName, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }

    private async Task<User> LockUserAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleAsync(ct);
        await db.Entry(user).ReloadAsync(ct); return user;
    }
}
