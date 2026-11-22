using MakanApp.Application.Commerce;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using Xunit;

namespace MakanApp.UnitTests.Commerce;

public sealed class OpenAccessCommerceTests
{
    [Theory]
    [InlineData(EntitlementKey.Academic)]
    [InlineData(EntitlementKey.Ai)]
    [InlineData(EntitlementKey.Storage)]
    public async Task OpenAccessFeatureDecisionDoesNotRequireOrInventSubscription(EntitlementKey key)
    {
        var store = new EmptyCommerceStore();
        var service = Service(store, false);
        Assert.True(await service.IsEnabledAsync(Guid.NewGuid(), key, default));
        Assert.Equal(0, store.SubscriptionReads);
        Assert.Null(await service.GetEffectiveSubscriptionAsync(Guid.NewGuid(), default));
        Assert.Equal(1, store.SubscriptionReads);
    }

    [Fact]
    public async Task ReenabledEnforcementStillRejectsMissingSubscription()
    {
        var store = new EmptyCommerceStore();
        var service = Service(store, true);
        var owner = Guid.NewGuid();
        Assert.False(await service.IsEnabledAsync(owner, EntitlementKey.Academic, default));
        var capacity = await service.CheckCapacityAsync(owner, QuotaKey.ActiveClasses, 1, default);
        Assert.False(capacity.Allowed);
        Assert.True(capacity.EnforcementEnabled);
        Assert.Equal(CommerceErrorCodes.SubscriptionRequired, capacity.DenialCode);
    }

    [Fact]
    public async Task OpenAccessCapacityPreservesActualCounterWithoutCreatingCommercialData()
    {
        var store = new EmptyCommerceStore();
        var result = await Service(store, false).CheckCapacityAsync(Guid.NewGuid(), QuotaKey.ActiveClasses, 100, default);
        Assert.True(result.Allowed);
        Assert.False(result.EnforcementEnabled);
        Assert.Null(result.SubscriptionId);
        Assert.Null(result.DenialCode);
        Assert.Null(result.Limit);
        Assert.Equal(7, result.CurrentUsage);
        Assert.Equal(0, store.SubscriptionReads);
        Assert.Equal(1, store.CounterReads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidSelectorAndAmountRemainInvalidInBothModes(bool enforce)
    {
        var store = new EmptyCommerceStore();
        var service = Service(store, enforce);
        var invalidId = await Assert.ThrowsAsync<CommerceException>(() => service.IsEnabledAsync(Guid.Empty, EntitlementKey.Ai, default));
        var invalidKey = await Assert.ThrowsAsync<CommerceException>(() => service.IsEnabledAsync(Guid.NewGuid(), (EntitlementKey)int.MaxValue, default));
        var invalidAmount = await Assert.ThrowsAsync<CommerceException>(() => service.CheckCapacityAsync(Guid.NewGuid(), QuotaKey.ActiveClasses, 0, default));
        Assert.All(new[] { invalidId, invalidKey, invalidAmount }, error => Assert.Equal(CommerceErrorCodes.UsageInvalid, error.Code));
        Assert.Equal(0, store.SubscriptionReads);
        Assert.Equal(0, store.CounterReads);
    }

    private static CommerceService Service(EmptyCommerceStore store, bool enforce) =>
        new(new UnusedAccessResolver(), store, TimeProvider.System, new CommercialOptions { EnforcementEnabled = enforce });

    private sealed class UnusedAccessResolver : IAccessContextResolver
    {
        public Task<AccessContext> ResolveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("این تست فقط تصمیم تجاری داخلی را بررسی می‌کند؛ مجوز منبع جداست.");
    }

    private sealed class EmptyCommerceStore : ICommerceStore
    {
        public int SubscriptionReads { get; private set; }
        public int CounterReads { get; private set; }
        public Task<CommerceSubscriptionSnapshot?> GetLatestSubscriptionAsync(Guid organizationId, DateTime utcNow, CancellationToken cancellationToken)
        {
            SubscriptionReads++;
            return Task.FromResult<CommerceSubscriptionSnapshot?>(null);
        }

        public Task<IReadOnlyDictionary<QuotaKey, long>> GetCurrentUsageAsync(Guid organizationId, Guid subscriptionId,
            IReadOnlyCollection<QuotaKey> quotaKeys, CancellationToken cancellationToken)
        {
            CounterReads++;
            return Task.FromResult<IReadOnlyDictionary<QuotaKey, long>>(quotaKeys.ToDictionary(key => key, _ => 7L));
        }

        public Task<UsageRecordStoreResult> RecordMeteredUsageAsync(Guid organizationId, QuotaKey key, long amount,
            Guid sourceOperationId, DateTime occurredAtUtc, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("بررسی دسترسی نامحدود نباید ثبت مصرف پولی بسازد.");
    }
}
