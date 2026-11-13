using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed partial class EfOrderPaymentStore
{
    private async Task<Guid> ResolveSchoolPlanAsync(Guid user, Guid applicationId, long? expectedVersion, Guid? organizationId, DateTime now, CancellationToken ct, bool creating = true)
    {
        var app = await dbContext.SchoolApplications.FromSqlInterpolated($"SELECT * FROM [organization].[SchoolApplications] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={applicationId}")
            .SingleOrDefaultAsync(a => a.ApplicantUserId == user, ct) ?? throw new SchoolApplicationException(SchoolApplicationErrorCodes.NotFound);
        if (organizationId.HasValue || app.Version != expectedVersion) throw new SchoolApplicationException(SchoolApplicationErrorCodes.Conflict);
        if (app.Status != SchoolApplicationStatus.Approved) throw new SchoolApplicationException(SchoolApplicationErrorCodes.NotApproved);
        if (creating)
        {
            var previous = await dbContext.Orders.SingleOrDefaultAsync(o => o.SchoolApplicationId == applicationId && o.Status != MakanApp.Domain.Commerce.OrderStatus.Cancelled, ct);
            if (previous is not null)
            {
                // فقط intent منقضیِ بدون هیچ تلاش بانکی جایگزین می‌شود؛ پرداخت یا نتیجهٔ نامطمئن هرگز کنار گذاشته نمی‌شود.
                if (previous.Status != MakanApp.Domain.Commerce.OrderStatus.PendingPayment || previous.TermsExpireAtUtc > now ||
                    !previous.TermsExpireAtUtc.HasValue || await dbContext.PaymentAttempts.AnyAsync(p => p.OrderId == previous.Id, ct))
                    throw new SchoolApplicationException(SchoolApplicationErrorCodes.Conflict);
                previous.Cancel(now); await dbContext.SaveChangesAsync(ct);
            }
        }
        var offering = await dbContext.SchoolEstablishmentOfferings.AsNoTracking().SingleOrDefaultAsync(o => o.Kind == app.Kind, ct)
            ?? throw new SchoolApplicationException(SchoolApplicationErrorCodes.CommercialTermsUnavailable);
        return offering.PlanVersionId;
    }
}
