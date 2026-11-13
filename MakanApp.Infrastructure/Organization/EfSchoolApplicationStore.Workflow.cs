using System.Data;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed partial class EfSchoolApplicationStore
{
    public async Task<SchoolApplicationResult> ChangeStatusAsync(SchoolApplicationActor actor, Guid id, SchoolApplicationActionCommand command, bool cancel, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockActor(actor, ct);
        var app = await Locked(actor, id, false, ct);
        var action = cancel ? "school_application.Cancelled" : "school_application.Submitted";
        var hash = Hash(actor, action + ":" + id, command);
        if (await Receipt(actor, command.ClientOperationId, hash, id, ct) is null)
        {
            if (app.Version != command.ExpectedVersion || (cancel ? !app.CanCancel : !app.CanEdit)) throw Error(SchoolApplicationErrorCodes.Conflict);
            if (cancel) app.Cancel(Now());
            else
            {
                var docs = await db.SchoolApplicationDocuments.Where(d => d.ApplicationId == id && d.Revision == app.Revision).ToArrayAsync(ct);
                if (app.SubmissionRequirements(docs.Select(d => d.Kind)).Count != 0) throw Error(SchoolApplicationErrorCodes.SubmissionIncomplete);
                // مدرکِ نسخهٔ فعلی باید هنوز آماده باشد؛ تأیید قدیمی فایل مجوز استفاده از فایل حذف‌شده نیست.
                foreach (var doc in docs)
                    if (!await db.FileAssets.AnyAsync(f => f.Id == doc.FileAssetId && f.Status == MakanApp.Domain.Storage.FileAssetStatus.Ready && f.UploadedByUserId == app.ApplicantUserId, ct))
                        throw Error(SchoolApplicationErrorCodes.FileUnavailable);
                app.Submit("school-establishment-v1-20261113", Now(), docs.Select(d => d.Kind));
            }
            Complete(actor, app, command.ClientOperationId, hash, action, command.ApplicantFeedback?.Trim());
            await db.SaveChangesAsync(ct);
        }
        var result = await Map(app, null, ct); await tx.CommitAsync(ct); return result;
    }

    public async Task<SchoolApplicationHistoryPage> HistoryAsync(SchoolApplicationActor actor, Guid id, bool reviewer, int page, int pageSize, CancellationToken ct)
    {
        await ReadAsync(actor, id, reviewer, null, ct);
        var query = db.SchoolApplicationHistories.AsNoTracking().Where(h => h.ApplicationId == id);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(h => h.OccurredAtUtc).ThenByDescending(h => h.Version).ThenBy(h => h.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(h => new SchoolApplicationHistoryResult(h.Id, h.Action, h.Revision, h.Version, h.Status, h.Reason, h.OccurredAtUtc, reviewer ? h.ActorUserId : null)).ToArrayAsync(ct);
        return new(items, total, page, pageSize);
    }

    private async Task<SchoolApplicationProgress> Progress(SchoolApplication app, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.SchoolApplicationId == app.Id && o.Status != MakanApp.Domain.Commerce.OrderStatus.Cancelled, ct);
        var request = await db.PlatformProvisioningRequests.AsNoTracking().SingleOrDefaultAsync(p => p.SchoolApplicationId == app.Id, ct);
        var configured = await db.SchoolEstablishmentOfferings.AnyAsync(o => o.Kind == app.Kind, ct);
        return new(order is not null ? "FrozenOnOrder" : configured ? "Configured" : "BlockedConfiguration", order?.Id, order?.Status.ToString(),
            request?.FailureCode == "INITIAL_SCHOOL_MANAGER_POLICY_REQUIRED" ? "BlockedManagerPolicy" : request?.Status.ToString() ??
                (order?.Status == MakanApp.Domain.Commerce.OrderStatus.Paid ? "PaidAwaitingProvisioning" : "NotStarted"),
            request?.Id, request?.OrganizationId, request?.SubscriptionId);
    }
}
