using MakanApp.Application.Identity;
using MakanApp.Domain.Academic;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed class PersonalClassAccountDeletionLifecycle(MakanDbContext db, TimeProvider clock) : IPersonalClassAccountDeletionLifecycle
{
    public async Task EnsureCanFreezeAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var ids = await db.PersonalClasses.Where(c => c.ArchivedAtUtc == null &&
            (c.ManagerUserId == context.UserId || c.ManagerUserId == null && c.HostUserId == context.UserId))
            .OrderBy(c => c.Id).Select(c => c.Id).ToArrayAsync(ct);
        foreach (var id in ids)
        {
            var classroom = await db.PersonalClasses.FromSqlInterpolated($"SELECT * FROM [academic].[PersonalClasses] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleAsync(ct);
            await db.Entry(classroom).ReloadAsync(ct);
            if (classroom.ArchivedAtUtc is null && (classroom.ManagerUserId == context.UserId || classroom.ManagerUserId is null && classroom.HostUserId == context.UserId))
                throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.LastPersonalClassManager);
        }
    }
    public async Task EndAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        var members = await db.PersonalClassParticipants.Where(p => p.StudentUserId == context.UserId && p.EndedAtUtc == null).ToArrayAsync(ct);
        foreach (var member in members) member.End(clock.GetUtcNow().UtcDateTime, AccountDeletionRequestPolicy);
    }
    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.PersonalClassParticipants.AnyAsync(p => p.StudentUserId == context.UserId && p.EndedAtUtc == null, ct) ||
            await db.PersonalClasses.AnyAsync(c => c.ArchivedAtUtc == null &&
                (c.ManagerUserId == context.UserId || c.ManagerUserId == null && c.HostUserId == context.UserId), ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.LastPersonalClassManager);
    }
    private const string AccountDeletionRequestPolicy = MakanApp.Domain.Identity.AccountDeletionRequest.CurrentPolicyVersion;
}
