using MakanApp.Application.Ai;
using MakanApp.Domain.Commerce;
using Xunit;

namespace MakanApp.UnitTests.Ai;

public sealed class AiUsagePolicyTests
{
    [Fact]
    public void MissingConfigurationRetainsCommercialCompatibility()
    {
        var policy = new AiUsageOptions();
        Assert.Equal(AiUsageMode.Commercial, policy.Mode);
        Assert.False(policy.IsUnmetered);
        Assert.Equal(AiPayerSelection.FreeTrial, policy.NormalizePayer(AiPayerSelection.FreeTrial));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(AiPayerSelection.Individual)]
    [InlineData(AiPayerSelection.Organization)]
    [InlineData(AiPayerSelection.FreeTrial)]
    public void UnmeteredPayerHasNoCommercialEffect(AiPayerSelection? payer) =>
        Assert.Null(new AiUsageOptions { Mode = AiUsageMode.Unmetered }.NormalizePayer(payer));

    [Theory]
    [InlineData(AiUsageMode.Commercial)]
    [InlineData(AiUsageMode.Unmetered)]
    public void UnknownPayerIsRejectedInEveryMode(AiUsageMode mode) =>
        Assert.Equal(AiServiceErrorCodes.InvalidRequest, Assert.Throws<AiServiceException>(() =>
            new AiUsageOptions { Mode = mode }.NormalizePayer((AiPayerSelection)999)).Code);

    [Fact]
    public void UnmeteredOperationIsExplicitDurableAndNeverPretendsToBeTrial()
    {
        var now = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc); var user = Guid.NewGuid();
        var hash = System.Security.Cryptography.SHA256.HashData([1, 2, 3]);
        var operation = UsageReservation.CreateUnmetered(user, CommerceContextType.Personal, user, Guid.NewGuid(), hash, now);
        hash[0] ^= 1;
        Assert.True(operation.IsUnmetered); Assert.False(operation.IsTrial);
        Assert.Equal(UsageAccountingMode.Unmetered, operation.AccountingMode);
        Assert.Null(operation.SubscriptionId); Assert.Null(operation.PlanVersionId);
        Assert.Equal(user, operation.OwnerId); Assert.NotEqual(hash, operation.PayloadHash);
        Assert.Equal(UsageReservationStatus.Reserved, operation.Status);
        Assert.True(operation.Finalize(UsageReservationStatus.Unknown, "provider_timeout", now.AddSeconds(1)));
        Assert.True(operation.Finalize(UsageReservationStatus.Consumed, "verified_response", now.AddDays(2)));
        var ledger = UsageLedgerEntry.FromReservation(operation, now.AddDays(2), "verified_response");
        Assert.True(ledger.IsUnmetered); Assert.Equal(operation.Id, ledger.ReservationId);
        Assert.Null(ledger.SubscriptionId); Assert.Null(ledger.PlanVersionId);
        Assert.False(operation.Finalize(UsageReservationStatus.Consumed, "verified_response", now.AddDays(2)));
        Assert.Throws<InvalidOperationException>(() => operation.Finalize(UsageReservationStatus.Released, "rewrite", now.AddDays(3)));
    }

    [Fact]
    public void UnmeteredOperationStillRequiresIdentityHashAndUtc()
    {
        var user = Guid.NewGuid(); var now = DateTime.UtcNow; var hash = new byte[32];
        Assert.Throws<ArgumentException>(() => UsageReservation.CreateUnmetered(Guid.Empty, CommerceContextType.Personal, user, Guid.NewGuid(), hash, now));
        Assert.Throws<ArgumentException>(() => UsageReservation.CreateUnmetered(user, CommerceContextType.Personal, user, Guid.Empty, hash, now));
        Assert.Throws<ArgumentException>(() => UsageReservation.CreateUnmetered(user, CommerceContextType.Personal, user, Guid.NewGuid(), [], now));
        Assert.Throws<ArgumentException>(() => UsageReservation.CreateUnmetered(user, CommerceContextType.Personal, user, Guid.NewGuid(), hash, DateTime.SpecifyKind(now, DateTimeKind.Unspecified)));
    }
}
