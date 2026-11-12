using System.Data;
using MakanApp.Application.Ai;
using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Domain.Intelligence;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class EfAssistantContextStore(MakanDbContext db, CommercialActorAccess actors) : IAssistantContextStore
{
    public Task<AssistantContextSelection?> GetAsync(Guid user, Guid session, CancellationToken ct) =>
        db.AssistantContextSelections.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == user && s.SessionId == session, ct);
    public async Task<AssistantContextSelection> SaveAsync(Guid user, Guid session, SelectAssistantContextCommand command, AccessContext context,
        DateTime now, bool onlyIfAbsent, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _ = await db.UserSessions.FromSqlInterpolated($"SELECT * FROM [identity].[UserSessions] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={session} AND [UserId]={user}")
            .AsNoTracking().Select(s => s.Id).SingleAsync(ct);
        await actors.EnsureAsync(user, session, ct);
        var selection = await db.AssistantContextSelections.SingleOrDefaultAsync(s => s.UserId == user && s.SessionId == session, ct);
        if (selection is not null && onlyIfAbsent) { await tx.CommitAsync(ct); return selection; }
        if (selection is not null && selection.LastClientOperationId == command.ClientOperationId)
        {
            if (selection.Kind != command.Kind || selection.MembershipId != command.MembershipId || selection.Role != command.Role ||
                selection.ClassId != command.ClassId || selection.SubjectOrganizationPersonId != command.SubjectOrganizationPersonId) throw Conflict();
            await tx.CommitAsync(ct); return selection;
        }
        if (selection is null ? command.ExpectedVersion is not null : command.ExpectedVersion != Convert.ToBase64String(selection.RowVersion)) throw Conflict();
        if (selection is null) { selection = AssistantContextSelection.Create(user, session); db.AssistantContextSelections.Add(selection); }
        selection.Select(command.Kind, context.OrganizationId, context.MembershipId, context.ActiveRole, context.SubjectOrganizationPersonId, command.ClassId, command.ClientOperationId, now);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return selection;
    }
    private static AiServiceException Conflict() => new(AssistantContextErrorCodes.Conflict, "زمینهٔ Assistant هم‌زمان تغییر کرده است؛ وضعیت را دوباره دریافت کنید.");
}
