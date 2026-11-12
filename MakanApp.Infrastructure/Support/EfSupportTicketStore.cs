using System.Data;
using System.Text.Json;
using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Application.Platform;
using MakanApp.Application.Storage;
using MakanApp.Application.Support;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using MakanApp.Domain.Storage;
using MakanApp.Domain.Support;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Support;

public sealed class EfSupportTicketStore(MakanDbContext db, CommercialActorAccess actors, IPlatformPermissionResolver permissions,
    IIdentitySecurity security, IFileAssetBoundAccessResolver files, TimeProvider clock,
    MakanApp.Infrastructure.Notification.ProductNotificationWriter notifications) : ISupportTicketStore
{
    public async Task<SupportTicketResult> CreateAsync(SupportActor actor, CreateSupportTicketCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor.UserId}").AsNoTracking().Select(u => u.Id).SingleAsync(ct);
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        var hash = Hash(actor, "create", command);
        var replay = await Receipt(actor, command.ClientOperationId, hash, null, ct);
        if (replay is not null) { var repeated = await ReadAsync(actor, replay.TicketId, false, ct); await tx.CommitAsync(ct); return repeated; }
        var now = Now(); var ticket = SupportTicket.Create(actor.UserId, command.Subject, command.Category, now);
        db.SupportTickets.Add(ticket);
        await AddMessageAsync(actor, ticket, command.Body, command.FileAssetIds, false, now, ct);
        Complete(actor, ticket, command.ClientOperationId, hash, "support.Created", null, now);
        await db.SaveChangesAsync(ct); var result = await Map(ticket, false, true, 0, ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<SupportTicketResult> ReplyAsync(SupportActor actor, Guid id, ReplySupportTicketCommand command, bool staff, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ticket = await Lock(actor, id, staff, true, ct);
        var hash = Hash(actor, $"reply:{id}:{staff}", command);
        var replay = await Receipt(actor, command.ClientOperationId, hash, id, ct);
        if (replay is null)
        {
            Version(ticket, command.ExpectedVersion); EnsureOpen(ticket);
            var now = Now(); ticket.Reply(staff, now);
            await AddMessageAsync(actor, ticket, command.Body, command.FileAssetIds, staff, now, ct);
            Complete(actor, ticket, command.ClientOperationId, hash, "support.Replied", null, now);
            await db.SaveChangesAsync(ct);
        }
        var result = await Map(ticket, staff, true, 0, ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<SupportTicketResult> ManageAsync(SupportActor actor, Guid id, ManageSupportTicketCommand command, bool resolve, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ticket = await Lock(actor, id, true, true, ct);
        var hash = Hash(actor, $"manage:{id}:{resolve}", command);
        if (await Receipt(actor, command.ClientOperationId, hash, id, ct) is null)
        {
            Version(ticket, command.ExpectedVersion); EnsureOpen(ticket);
            var now = Now();
            if (resolve) ticket.Resolve(now);
            else
            {
                var target = command.AssignedStaffUserId!.Value;
                if (!await db.Users.AnyAsync(u => u.Id == target && u.AccountStatus == UserAccountStatus.Active, ct) ||
                    !await permissions.HasPermissionAsync(target, PlatformPermissionKeys.SupportRead, ct) ||
                    !await permissions.HasPermissionAsync(target, PlatformPermissionKeys.SupportManage, ct)) throw new SupportException(SupportErrorCodes.Forbidden);
                ticket.Assign(target, now);
            }
            Complete(actor, ticket, command.ClientOperationId, hash, resolve ? "support.Resolved" : "support.Assigned", command.Reason.Trim(), now);
            await db.SaveChangesAsync(ct);
        }
        var result = await Map(ticket, true, true, 0, ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<SupportTicketResult> ReadAsync(SupportActor actor, Guid id, bool staff, CancellationToken ct, long afterMessageVersion = 0)
    {
        await AuthorizeStaff(actor, staff, false, ct);
        var ticket = await db.SupportTickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && (staff || t.OwnerUserId == actor.UserId), ct)
            ?? throw new SupportException(SupportErrorCodes.NotFound);
        return await Map(ticket, staff, true, afterMessageVersion, ct);
    }
    public async Task<SupportTicketPage> PageAsync(SupportActor actor, bool staff, SupportTicketStatus? status, int page, int pageSize, CancellationToken ct)
    {
        await AuthorizeStaff(actor, staff, false, ct);
        var query = db.SupportTickets.AsNoTracking().Where(t => staff || t.OwnerUserId == actor.UserId);
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(t => t.UpdatedAtUtc).ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var results = new List<SupportTicketResult>(); foreach (var ticket in rows) results.Add(await Map(ticket, staff, false, 0, ct));
        return new(results, count, page, pageSize);
    }
    private async Task<SupportTicket> Lock(SupportActor actor, Guid id, bool staff, bool write, CancellationToken ct)
    {
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [identity].[Users] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={actor.UserId}").AsNoTracking().Select(u => u.Id).SingleAsync(ct);
        await AuthorizeStaff(actor, staff, write, ct);
        var ticket = await db.SupportTickets.FromSqlInterpolated($"SELECT * FROM [support].[SupportTickets] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}")
            .SingleOrDefaultAsync(t => staff || t.OwnerUserId == actor.UserId, ct) ?? throw new SupportException(SupportErrorCodes.NotFound);
        return ticket;
    }
    private async Task AuthorizeStaff(SupportActor actor, bool staff, bool write, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (staff && (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.SupportRead, ct) ||
            write && !await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.SupportManage, ct))) throw new SupportException(SupportErrorCodes.Forbidden);
    }
    private async Task AddMessageAsync(SupportActor actor, SupportTicket ticket, string body, IReadOnlyCollection<Guid>? ids, bool staff, DateTime now, CancellationToken ct)
    {
        var message = SupportTicketMessage.Create(ticket.Id, actor.UserId, staff, body, ticket.Version, now);
        foreach (var id in ids ?? [])
        {
            var file = await db.FileAssets.FromSqlInterpolated($"SELECT * FROM [storage].[FileAssets] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={id}").SingleOrDefaultAsync(ct);
            if (file is null || file.UploadedByUserId != actor.UserId || file.OrganizationId.HasValue || file.PersonalClassId.HasValue ||
                file.Status != FileAssetStatus.Ready || file.UnattachedExpiresAtUtc <= now || await files.IsBoundAsync(id, ct)) throw new SupportException(SupportErrorCodes.FileUnavailable);
            file.Retain(now); db.SupportTicketAttachments.Add(SupportTicketAttachment.Create(ticket.Id, message.Id, id));
        }
        db.SupportTicketMessages.Add(message);
    }
    private async Task<SupportTicketOperation?> Receipt(SupportActor actor, Guid operation, string hash, Guid? ticket, CancellationToken ct)
    {
        var receipt = await db.SupportTicketOperations.AsNoTracking().SingleOrDefaultAsync(o => o.ActorUserId == actor.UserId && o.ClientOperationId == operation, ct);
        if (receipt is not null && (receipt.Fingerprint != hash || ticket.HasValue && receipt.TicketId != ticket)) throw new SupportException(SupportErrorCodes.Conflict);
        return receipt;
    }
    private void Complete(SupportActor actor, SupportTicket ticket, Guid operation, string hash, string action, string? reason, DateTime now)
    {
        if (actor.UserId != ticket.OwnerUserId) notifications.SupportUpdated(operation, ticket.Id, ticket.OwnerUserId);
        if (ticket.AssignedStaffUserId is Guid assignee && assignee != actor.UserId && assignee != ticket.OwnerUserId)
            notifications.SupportUpdated(operation, ticket.Id, assignee);
        db.SupportTicketOperations.Add(SupportTicketOperation.Create(actor.UserId, operation, ticket.Id, hash, ticket.Version, now));
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actor.UserId, null, actor.SessionId, action, "SupportTicket", ticket.Id.ToString(), null,
            reason ?? "ثبت عملیات پشتیبانی توسط عامل مجاز", now, actor.CorrelationId, AdminAuditResult.Succeeded, null, null));
    }
    private async Task<SupportTicketResult> Map(SupportTicket ticket, bool staff, bool messages, long after, CancellationToken ct)
    {
        var rows = messages ? await db.SupportTicketMessages.AsNoTracking().Where(m => m.TicketId == ticket.Id && m.TicketVersion > after)
            .OrderBy(m => m.TicketVersion).Take(101).ToArrayAsync(ct) : [];
        var ids = rows.Take(100).Select(m => m.Id).ToArray();
        var attachments = messages ? await db.SupportTicketAttachments.AsNoTracking().Where(a => a.TicketId == ticket.Id && ids.Contains(a.MessageId)).ToArrayAsync(ct) : [];
        return new(ticket.Id, ticket.Subject, ticket.Category, ticket.Status, ticket.Version, ticket.CreatedAtUtc, ticket.UpdatedAtUtc,
            staff ? ticket.OwnerUserId : null, staff ? ticket.AssignedStaffUserId : null,
            rows.Take(100).Select(m => new SupportMessageResult(m.Id, m.IsStaff ? "Support" : "Owner", m.Body, m.TicketVersion, m.CreatedAtUtc,
                attachments.Where(a => a.MessageId == m.Id).Select(a => a.FileAssetId).ToArray())).ToArray(), rows.Length > 100, rows.Length == 0 ? null : rows[Math.Min(rows.Length, 100) - 1].TicketVersion);
    }
    private string Hash(SupportActor actor, string action, object command) => Convert.ToHexString(security.HashSessionToken("support:v1:" + actor.UserId + ":" + action + ":" + JsonSerializer.Serialize(command)));
    private static void Version(SupportTicket ticket, long version) { if (ticket.Version != version) throw new SupportException(SupportErrorCodes.Conflict); }
    private static void EnsureOpen(SupportTicket ticket) { if (ticket.Status == SupportTicketStatus.Resolved) throw new SupportException(SupportErrorCodes.Conflict); }
    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
