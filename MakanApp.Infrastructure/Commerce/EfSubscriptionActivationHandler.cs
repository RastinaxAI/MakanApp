using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed class EfSubscriptionActivationHandler(
    MakanDbContext dbContext,
    TimeProvider timeProvider) : ISubscriptionActivationHandler
{
    public async Task HandleAsync(
        SubscriptionActivationRequested payload,
        CancellationToken cancellationToken)
    {
        if (payload.OrderId == Guid.Empty ||
            payload.OrganizationId == Guid.Empty ||
            payload.PlanVersionId == Guid.Empty)
        {
            throw new InvalidOperationException("payload فعال‌سازی اشتراک معتبر نیست.");
        }

        var alreadyApplied = await dbContext.Subscriptions.AnyAsync(
            subscription => subscription.SourceOrderId == payload.OrderId,
            cancellationToken);
        if (alreadyApplied)
        {
            return;
        }

        var planVersion = await dbContext.PlanVersions.SingleOrDefaultAsync(
            version => version.Id == payload.PlanVersionId,
            cancellationToken);
        if (planVersion is null || !planVersion.IsPublished)
        {
            throw new InvalidOperationException("نسخه پلن رویداد فعال‌سازی منتشرشده نیست.");
        }

        var current = await dbContext.Subscriptions
            .FromSqlInterpolated($"SELECT * FROM [commerce].[Subscriptions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {payload.OrganizationId} AND [Status] = {(int)SubscriptionStatus.Active}")
            .SingleOrDefaultAsync(cancellationToken);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        current?.Cancel(nowUtc);
        var subscription = Subscription.CreatePending(
            payload.OrganizationId,
            planVersion,
            nowUtc,
            null,
            nowUtc,
            payload.OrderId);
        subscription.Activate(nowUtc);
        dbContext.Subscriptions.Add(subscription);
    }
}
