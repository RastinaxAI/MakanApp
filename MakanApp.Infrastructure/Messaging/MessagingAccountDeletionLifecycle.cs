using MakanApp.Application.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingAccountDeletionLifecycle(MakanDbContext db, TimeProvider clock) : IMessagingAccountDeletionLifecycle
{
    public async Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        // همان قاعدهٔ خروج مالک: انتقال مالکیت یا بایگانی باید با جریان موجود انجام شود.
        var ids = await OwnedActiveConversations(context.UserId).OrderBy(id => id).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            _ = await db.Conversations.FromSqlInterpolated($"SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
                .AsNoTracking().SingleAsync(ct);
        }
        if (await OwnedActiveConversations(context.UserId).AnyAsync(ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.ConversationOwnership);
    }

    public async Task PurgePrivateStateAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var otherOwners = await db.AddressBookEntries.Where(e => e.OwnerUserId != context.UserId && e.LinkedAccountUserId == context.UserId)
            .Select(e => e.OwnerUserId).Distinct().ToArrayAsync(ct);
        // ترتیب قفل والدها با عملیات همگام‌سازی دفترچه سازگار است؛ محتوای رمز‌شدهٔ دیگران بازنویسی نمی‌شود.
        foreach (var owner in otherOwners.Append(context.UserId).Distinct().OrderBy(id => id))
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={owner}")
                .AsNoTracking().Select(u => u.Id).SingleAsync(ct);
        foreach (var owner in otherOwners.OrderBy(id => id))
        {
            var state = await db.ContactSyncStates.SingleOrDefaultAsync(s => s.OwnerUserId == owner, ct);
            if (state is null) { state = ContactSyncState.Create(owner); db.ContactSyncStates.Add(state); }
            var links = await db.AddressBookEntries.Where(e => e.OwnerUserId == owner && e.LinkedAccountUserId == context.UserId && e.DeletedAtUtc == null)
                .OrderBy(e => e.Id).ToArrayAsync(ct);
            foreach (var entry in links)
                entry.Update(entry.ProtectedContent, entry.HasManualName, null, state.NextVersion(), clock.GetUtcNow().UtcDateTime);
        }
        await db.ContactDeviceBindings.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.ContactSyncOperations.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.ContactPhones.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.AddressBookEntries.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.ContactSyncDevices.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.ContactSyncStates.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.UserContacts.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.ConversationUserStates.Where(e => e.UserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.ChatFolders.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.SavedMessageOrganizations.Where(e => e.OwnerUserId == context.UserId).ExecuteDeleteAsync(ct);
        await db.MessagingPrivacySettings.Where(e => e.UserId == context.UserId).ExecuteDeleteAsync(ct);
        // پیام، پیوست، receipt، participant و block تاریخچهٔ مشترک/ایمنی‌اند و حذف نمی‌شوند.
    }

    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.AddressBookEntries.AnyAsync(e => e.OwnerUserId == context.UserId || e.LinkedAccountUserId == context.UserId, ct) ||
            await db.ContactPhones.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.ContactSyncDevices.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.ContactDeviceBindings.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.ContactSyncOperations.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.ContactSyncStates.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.UserContacts.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.ChatFolders.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.SavedMessageOrganizations.AnyAsync(e => e.OwnerUserId == context.UserId, ct) ||
            await db.ConversationUserStates.AnyAsync(e => e.UserId == context.UserId, ct) ||
            await db.MessagingPrivacySettings.AnyAsync(e => e.UserId == context.UserId, ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
        await EnsureCanFreezeAsync(context, ct);
    }

    private IQueryable<Guid> OwnedActiveConversations(Guid user) =>
        from p in db.ConversationParticipants join c in db.Conversations on p.ConversationId equals c.Id
        where p.UserId == user && p.Role == ConversationParticipantRole.Owner && p.Status == ConversationParticipantStatus.Active &&
            p.EndedAtUtc == null && c.Status == ConversationStatus.Active && c.ManagementPolicy == ConversationManagementPolicy.UserManaged
        select c.Id;
}
