using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed class EfSubscriptionActivationHandler(MakanDbContext dbContext, EfCommercialStore commerce, TimeProvider timeProvider,
    MakanApp.Infrastructure.Realtime.RealtimeInvalidationWriter realtime)
    : ISubscriptionActivationHandler
{
    public async Task HandleAsync(SubscriptionActivationRequested payload, CancellationToken ct)
    {
        var order = await dbContext.Orders.SingleOrDefaultAsync(o => o.Id == payload.OrderId, ct);
        if (order is null || order.Status != OrderStatus.Paid || order.PlanVersionId != payload.PlanVersionId || !order.PaidAtUtc.HasValue)
            throw new InvalidOperationException("سفارش معتبر تسویه‌شده برای فعال‌سازی لازم است.");
        var owner = order.OrganizationId.HasValue ? SubscriptionOwner.Organization(order.OrganizationId.Value) : SubscriptionOwner.Individual(order.PurchaserUserId);
        await commerce.LockOwnerAsync(owner, ct);
        if (await dbContext.Subscriptions.AnyAsync(s => s.SourceOrderId == order.Id, ct)
            || await dbContext.SubscriptionAmendments.AnyAsync(a => a.OrderId == order.Id, ct)) return;
        var version = await commerce.VersionAsync(order.PlanVersionId, ct);
        if (!version.IsPublished) throw new InvalidOperationException("نسخهٔ منتشرشده لازم است.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!order.HasCommercialTerms)
        {
            if (!order.OrganizationId.HasValue) throw new InvalidOperationException("سفارش قدیمی نیازمند فرآیند مالکیت معتبر است.");
            var previous = await dbContext.Subscriptions.SingleOrDefaultAsync(s => s.OrganizationId == order.OrganizationId && s.Status == SubscriptionStatus.Active, ct);
            if (previous is { CommercialControlEnabled: true }) throw new InvalidOperationException("سفارش قدیمی حق بازنویسی قرارداد مدیریت‌شده را ندارد.");
            previous?.Cancel(now);
            var legacy = Subscription.CreatePending(order.OrganizationId.Value, version, now, null, now, order.Id);
            legacy.Activate(now); dbContext.Subscriptions.Add(legacy); realtime.CaptureCommerce(); return;
        }
        if (order.Purpose == CommercialOrderPurpose.Upgrade)
        {
            var target = await dbContext.Subscriptions.SingleAsync(s => s.Id == order.TargetSubscriptionId && s.OwnerType == owner.Type && s.OwnerId == owner.Id, ct);
            if (target.PlanVersionId != order.ExpectedPreviousPlanVersionId || !target.IsEffectiveAt(now))
                throw new InvalidOperationException("شرایط ارتقای پرداخت‌شده نیازمند تطبیق است؛ حقوق قبلی بازنویسی نشد.");
            var previous = await commerce.VersionAsync(target.PlanVersionId, ct);
            CommercialPlanChange.EnsureUpgrade(previous, version);
            dbContext.SubscriptionAmendments.Add(SubscriptionAmendment.Create(target, version, order.Id, now, order.Amount / 10m, order.CreatedAtUtc));
            target.ApplyUpgrade(version, now); realtime.CaptureCommerce(); return;
        }
        var startsUponPayment = order.Purpose == CommercialOrderPurpose.Purchase
            || order.Purpose == CommercialOrderPurpose.Renewal && order.BillingPeriodIndex == 0 && order.BillingAnchorUtc == order.CreatedAtUtc;
        var anchor = startsUponPayment ? order.PaidAtUtc.Value : order.BillingAnchorUtc!.Value;
        var monthly = Subscription.CreateMonthly(owner, version, anchor, order.BillingPeriodIndex, now,
            order.Id, owner.Type == SubscriptionOwnerType.Individual || await dbContext.Subscriptions.AnyAsync(s => s.OwnerType == owner.Type
                && s.OwnerId == owner.Id && s.CommercialControlEnabled, ct));
        if (monthly.EndsAtUtc <= now) throw new InvalidOperationException("پرداخت دورهٔ پایان‌یافته نیازمند تطبیق است؛ حق جدیدِ نامعتبر ایجاد نشد.");
        var overlap = await dbContext.Subscriptions.Where(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id
            && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled)
            && (s.CommercialControlEnabled || s.OwnerType == SubscriptionOwnerType.Individual)
            && s.EndsAtUtc > monthly.StartsAtUtc && s.StartsAtUtc < monthly.EndsAtUtc).AnyAsync(ct);
        if (overlap) throw new InvalidOperationException("پرداخت هم‌پوشان نیازمند تطبیق است؛ دوره یا سهمیهٔ دوم ایجاد نشد.");
        if (monthly.StartsAtUtc <= now)
        {
            var old = await dbContext.Subscriptions.Where(s => s.OwnerType == owner.Type && s.OwnerId == owner.Id && s.Status == SubscriptionStatus.Active).ToArrayAsync(ct);
            foreach (var item in old) item.Cancel(now);
        }
        dbContext.Subscriptions.Add(monthly);
        realtime.CaptureCommerce();
    }
}
