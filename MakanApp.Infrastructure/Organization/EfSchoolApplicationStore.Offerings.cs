using System.Data;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed partial class EfSchoolApplicationStore
{
    public async Task<IReadOnlyList<SchoolOfferingResult>> OfferingsAsync(SchoolApplicationActor actor, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct); await Reviewer(actor, false, ct);
        if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.CommerceManage, ct)) throw Error(SchoolApplicationErrorCodes.Forbidden);
        return await db.SchoolEstablishmentOfferings.AsNoTracking().OrderBy(o => o.Kind).Select(o => new SchoolOfferingResult(o.Kind, o.PlanVersionId, o.Version)).ToArrayAsync(ct);
    }
    public async Task<SchoolOfferingResult> ConfigureOfferingAsync(SchoolApplicationActor actor, SchoolApplicationKind kind, SchoolOfferingCommand command, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await LockActor(actor, ct);
        await Reviewer(actor, true, ct);
        if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.CommerceManage, ct)) throw Error(SchoolApplicationErrorCodes.Forbidden);
        var hash = Hash(actor, "offering:" + kind, command);
        var receipt = await db.SchoolOfferingOperations.SingleOrDefaultAsync(o => o.ActorUserId == actor.UserId && o.ClientOperationId == command.ClientOperationId, ct);
        if (receipt is not null)
        {
            if (receipt.Fingerprint != hash) throw Error(SchoolApplicationErrorCodes.Conflict);
            await tx.CommitAsync(ct); return new(receipt.Kind, receipt.PlanVersionId, receipt.OfferingVersion);
        }
        var offering = await db.SchoolEstablishmentOfferings.FromSqlInterpolated($"SELECT * FROM [commerce].[SchoolEstablishmentOfferings] WITH (UPDLOCK,HOLDLOCK) WHERE [Kind]={(int)kind}").SingleOrDefaultAsync(ct);
        if ((offering?.Version ?? 0) != command.ExpectedVersion) throw Error(SchoolApplicationErrorCodes.Conflict);
        var version = await db.PlanVersions.Include(v => v.Price).Include(v => v.Entitlements).Include(v => v.QuotaDefinitions).SingleOrDefaultAsync(v => v.Id == command.PlanVersionId, ct);
        if (version is null || !version.IsPublished || version.ValidatePublication(SubscriptionOwnerType.Organization).Count != 0 ||
            !await db.Plans.AnyAsync(p => p.Id == version.PlanId && p.Audience == SubscriptionOwnerType.Organization && p.NewPurchaseAvailable, ct))
            throw Error(SchoolApplicationErrorCodes.CommercialTermsUnavailable);
        if (await db.Subscriptions.AnyAsync(s => s.PlanVersionId == version.Id, ct) ||
            await db.Orders.AnyAsync(o => o.PlanVersionId == version.Id && o.SchoolApplicationId == null, ct))
            throw Error(SchoolApplicationErrorCodes.CommercialTermsUnavailable);
        if (offering is null) { offering = SchoolEstablishmentOffering.Create(kind, version.Id, Now()); db.SchoolEstablishmentOfferings.Add(offering); }
        else offering.Change(version.Id, Now());
        db.SchoolOfferingOperations.Add(SchoolOfferingOperation.Create(actor.UserId, command.ClientOperationId, offering, hash));
        db.AdminAuditLogs.Add(AdminAuditLog.Create(actor.UserId, null, actor.SessionId, "school_offering.Configured", "SchoolEstablishmentOffering", kind.ToString(), null,
            command.Reason.Trim(), Now(), actor.CorrelationId, AdminAuditResult.Succeeded, null, null));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(kind, offering.PlanVersionId, offering.Version);
    }
}
