using System.Data;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed partial class EfSchoolApplicationStore
{
    public async Task<SchoolApplicationResult> ConfirmInitialManagerAsync(SchoolApplicationActor actor, Guid id, SchoolInitialManagerCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockActor(actor, ct); await RepairPermission(actor, ct);
        var app = await Locked(actor, id, true, ct); var hash = Hash(actor, "initial-manager:" + id, command);
        if (await Receipt(actor, command.ClientOperationId, hash, id, ct) is null)
        {
            if (app.Version != command.ExpectedVersion || app.Status != SchoolApplicationStatus.Approved || app.FinalInitialManagerUserId.HasValue)
                throw Error(SchoolApplicationErrorCodes.Conflict);
            await EligibleManager(command.InitialManagerUserId, ct);
            app.ConfirmMissingInitialManager(command.InitialManagerUserId, actor.UserId, Now());
            db.SchoolApplicationDecisions.Add(SchoolApplicationDecision.Create(app, actor.UserId, command.Reason.Trim(), Now()));
            Complete(actor, app, command.ClientOperationId, hash, "school_application.InitialManagerConfirmed", command.Reason.Trim());
            await db.SaveChangesAsync(ct);
            var paid = await db.Orders.SingleOrDefaultAsync(o => o.SchoolApplicationId == id && o.Status == OrderStatus.Paid, ct);
            if (paid is not null) await provisioning.PreparePaidOrderAsync(paid.Id, ct);
            await db.SaveChangesAsync(ct);
        }
        var result = await Map(app, null, ct, true); await tx.CommitAsync(ct); return result;
    }

    public async Task<SchoolApplicationResult> RetryProvisioningAsync(SchoolApplicationActor actor, Guid id, SchoolProvisioningRetryCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockActor(actor, ct); await RepairPermission(actor, ct);
        var app = await Locked(actor, id, true, ct); var hash = Hash(actor, "provisioning-retry:" + id, command);
        if (await Receipt(actor, command.ClientOperationId, hash, id, ct) is null)
        {
            if (app.Version != command.ExpectedVersion || app.Status != SchoolApplicationStatus.Approved || !app.FinalInitialManagerUserId.HasValue)
                throw Error(SchoolApplicationErrorCodes.Conflict);
            await EligibleManager(app.FinalInitialManagerUserId, ct);
            var order = await db.Orders.SingleOrDefaultAsync(o => o.SchoolApplicationId == id && o.Status == OrderStatus.Paid, ct)
                ?? throw Error(SchoolApplicationErrorCodes.Conflict);
            var requestId = await provisioning.PreparePaidOrderAsync(order.Id, ct);
            await db.SaveChangesAsync(ct);
            await provisioning.RequeueAsync(requestId, command.ClientOperationId, ct);
            app.RecordProvisioningRetry(Now());
            Complete(actor, app, command.ClientOperationId, hash, "school_application.ProvisioningRetryRequested", command.Reason.Trim());
            await db.SaveChangesAsync(ct);
        }
        var result = await Map(app, null, ct, true); await tx.CommitAsync(ct); return result;
    }

    private async Task RepairPermission(SchoolApplicationActor actor, CancellationToken ct)
    {
        await Reviewer(actor, true, ct);
        if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.ProvisioningManage, ct)) throw Error(SchoolApplicationErrorCodes.Forbidden);
    }
}
