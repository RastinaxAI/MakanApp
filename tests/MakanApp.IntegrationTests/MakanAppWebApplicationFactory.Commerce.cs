using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreateCommercePlanAsync(string? displayName = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var plan = Plan.Create(
            $"test.{Guid.NewGuid():N}",
            displayName ?? $"Test Plan {Guid.NewGuid():N}",
            Clock.GetUtcNow().UtcDateTime.AddHours(-3));
        dbContext.Plans.Add(plan);
        await dbContext.SaveChangesAsync();
        return plan.Id;
    }

    public async Task<Guid> CreatePublishedPlanVersionAsync(
        Guid planId,
        int versionNumber,
        IReadOnlyDictionary<EntitlementKey, bool>? entitlements = null,
        IReadOnlyDictionary<QuotaKey, long?>? quotas = null,
        decimal? price = null,
        CurrencyCode currency = CurrencyCode.Irr)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = Clock.GetUtcNow().UtcDateTime;
        var version = PlanVersion.CreateDraft(planId, versionNumber, nowUtc.AddHours(-2));
        foreach (var entitlement in entitlements ?? new Dictionary<EntitlementKey, bool>())
        {
            version.SetEntitlement(entitlement.Key, entitlement.Value);
        }

        foreach (var quota in quotas ?? new Dictionary<QuotaKey, long?>())
        {
            version.SetQuota(quota.Key, quota.Value);
        }

        if (price.HasValue)
        {
            version.SetPrice(price.Value, currency);
        }

        version.Publish(nowUtc.AddHours(-1));
        dbContext.PlanVersions.Add(version);
        await dbContext.SaveChangesAsync();
        return version.Id;
    }

    public async Task<Guid> CreateDraftPricedPlanVersionAsync(
        Guid planId,
        int versionNumber,
        decimal price)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var version = PlanVersion.CreateDraft(
            planId,
            versionNumber,
            Clock.GetUtcNow().UtcDateTime.AddMinutes(-1));
        version.SetPrice(price, CurrencyCode.Irr);
        dbContext.PlanVersions.Add(version);
        await dbContext.SaveChangesAsync();
        return version.Id;
    }

    public async Task<int> CountPaymentsAsync(Guid orderId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Payments.CountAsync(payment => payment.OrderId == orderId);
    }

    public async Task<int> CountPaymentAttemptsAsync(Guid orderId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.PaymentAttempts.CountAsync(attempt => attempt.OrderId == orderId);
    }

    public async Task<int> CountSubscriptionsForOrderAsync(Guid orderId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Subscriptions.CountAsync(subscription => subscription.SourceOrderId == orderId);
    }

    public async Task<int> CountCommerceActivationOutboxAsync(Guid orderId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var orderToken = orderId.ToString();
        return await dbContext.OutboxMessages.CountAsync(message =>
            message.MessageType == SubscriptionActivationOutboxContract.MessageType &&
            message.Payload.Contains(orderToken));
    }

    public async Task<Guid> CreateSubscriptionAsync(
        Guid organizationId,
        Guid planVersionId,
        SubscriptionStatus status,
        DateTime startsAtUtc,
        DateTime? endsAtUtc)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var version = await dbContext.PlanVersions.SingleAsync(item => item.Id == planVersionId);
        var subscription = Subscription.CreatePending(
            organizationId,
            version,
            startsAtUtc,
            endsAtUtc,
            startsAtUtc.AddMinutes(-1));
        switch (status)
        {
            case SubscriptionStatus.Pending:
                break;
            case SubscriptionStatus.Active:
                subscription.Activate(startsAtUtc);
                break;
            case SubscriptionStatus.Expired:
                subscription.Activate(startsAtUtc);
                subscription.Expire(endsAtUtc!.Value);
                break;
            case SubscriptionStatus.Cancelled:
                subscription.Cancel(startsAtUtc);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync();
        return subscription.Id;
    }

    public async Task<int> CountUsageEntriesAsync(
        Guid subscriptionId,
        QuotaKey key,
        Guid? sourceOperationId = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UsageLedgerEntries.CountAsync(entry =>
            entry.SubscriptionId == subscriptionId &&
            entry.QuotaKey == key &&
            (!sourceOperationId.HasValue || entry.SourceOperationId == sourceOperationId.Value));
    }

    public async Task CancelSubscriptionAsync(Guid subscriptionId, DateTime cancelledAtUtc)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var subscription = await dbContext.Subscriptions.SingleAsync(item => item.Id == subscriptionId);
        subscription.Cancel(cancelledAtUtc);
        await dbContext.SaveChangesAsync();
    }

    public async Task<long> SumUsageAsync(Guid subscriptionId, QuotaKey key)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UsageLedgerEntries
            .Where(entry => entry.SubscriptionId == subscriptionId && entry.QuotaKey == key)
            .SumAsync(entry => (long?)entry.Amount) ?? 0;
    }

    public async Task<CommerceUsageRecord> GetUsageEntryAsync(
        Guid subscriptionId,
        QuotaKey key,
        Guid sourceOperationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UsageLedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.SubscriptionId == subscriptionId &&
                entry.QuotaKey == key &&
                entry.SourceOperationId == sourceOperationId)
            .Select(entry => new CommerceUsageRecord(
                entry.Id,
                entry.OrganizationId,
                entry.SubscriptionId,
                entry.QuotaKey,
                entry.Amount,
                entry.SourceOperationId,
                entry.OccurredAtUtc))
            .SingleAsync();
    }

    public async Task<bool> DuplicatePlanVersionIsRejectedAsync(Guid planId, int versionNumber)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = Clock.GetUtcNow().UtcDateTime;
        var duplicate = PlanVersion.CreateDraft(planId, versionNumber, nowUtc);
        dbContext.PlanVersions.Add(duplicate);
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }

    public async Task<bool> SecondActiveSubscriptionIsRejectedAsync(
        Guid organizationId,
        Guid planVersionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = Clock.GetUtcNow().UtcDateTime;
        var version = await dbContext.PlanVersions.SingleAsync(item => item.Id == planVersionId);
        var duplicate = Subscription.CreatePending(
            organizationId,
            version,
            nowUtc,
            nowUtc.AddDays(30),
            nowUtc.AddMinutes(-1));
        duplicate.Activate(nowUtc);
        dbContext.Subscriptions.Add(duplicate);
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }
}

public sealed record CommerceUsageRecord(
    Guid Id,
    Guid OrganizationId,
    Guid SubscriptionId,
    QuotaKey QuotaKey,
    long Amount,
    Guid SourceOperationId,
    DateTime OccurredAtUtc);
