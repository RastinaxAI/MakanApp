using MakanApp.Application.Commerce;
using MakanApp.Application.Identity;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Commerce;

public sealed class CommerceAccountDeletionLifecycle(MakanDbContext db, EfCommercialStore commerce, TimeProvider clock) : ICommerceAccountDeletionLifecycle
{
    public async Task CloseAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        // همان قفل مالک با مسیر fulfillment مشترک است؛ inactive بودن در این lifecycle داخلی عمدی است.
        await commerce.LockOwnerAsync(SubscriptionOwner.Individual(context.UserId), ct, requireActive: false);
        var subscriptions = await db.Subscriptions.Where(s => s.OwnerType == SubscriptionOwnerType.Individual && s.OwnerId == context.UserId &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled || s.Status == SubscriptionStatus.Pending)).ToArrayAsync(ct);
        foreach (var subscription in subscriptions) subscription.Cancel(clock.GetUtcNow().UtcDateTime);
        // سفارش، پرداخت و Usage Reserved/Unknown/Consumed تغییر نمی‌کنند؛ هیچ refund ساخته نمی‌شود.
    }
    public async Task VerifyAsync(AccountDeletionExecutionContext context, CancellationToken ct)
    {
        if (await db.Subscriptions.AnyAsync(s => s.OwnerType == SubscriptionOwnerType.Individual && s.OwnerId == context.UserId &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled || s.Status == SubscriptionStatus.Pending), ct))
            throw new AccountDeletionExecutionException(AccountDeletionErrorCodes.InvariantFailed);
    }
}
