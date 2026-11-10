using MakanApp.Domain.Commerce;
using Xunit;

namespace MakanApp.UnitTests.Commerce;

public sealed class PaymentRecoveryDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public void ExpiredInitiationLeaseBecomesUnknownAndCannotIssueSecondPost()
    {
        var attempt = Attempt(); var claim = Guid.NewGuid();
        Assert.True(attempt.TryClaimInitiation(claim, Now, Now.AddSeconds(30)));
        Assert.False(attempt.TryClaimInitiation(Guid.NewGuid(), Now.AddSeconds(31), Now.AddSeconds(61)));
        Assert.Equal(PaymentAttemptStatus.Unknown, attempt.Status); Assert.Null(attempt.InitiationClaimToken);
        Assert.False(attempt.TryClaimInitiation(Guid.NewGuid(), Now.AddMinutes(5), Now.AddMinutes(6)));
    }
    [Fact]
    public void UncertainVerificationCannotClearActiveInitiationClaimOrReplaceDefinitiveFailure()
    {
        var attempt = Attempt(); var claim = Guid.NewGuid(); attempt.TryClaimInitiation(claim, Now, Now.AddSeconds(30));
        attempt.MarkVerificationUnknown(null, "UNKNOWN", Now);
        Assert.Equal(claim, attempt.InitiationClaimToken);
        attempt.MarkInitiationFailed(claim, "DECLINED", Now);
        attempt.MarkVerificationUnknown(null, "UNKNOWN", Now.AddSeconds(5));
        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status); Assert.Equal("DECLINED", attempt.FailureCode);
    }
    [Fact]
    public void BankReferenceIsCopiedIntoImmutableReceiptAndSuccessCannotBeDowngraded()
    {
        var attempt = Attempt(); attempt.MarkSucceeded("90001", Now, "777123456", Now);
        var payment = Payment.Create(attempt, Now);
        Assert.Equal("90001", payment.ProviderReference); Assert.Equal("777123456", payment.SettlementReference);
        Assert.Equal(Now, payment.ProviderPaidAtUtc);
        Assert.Throws<InvalidOperationException>(() => attempt.MarkVerificationFailed("90001", "DECLINED", Now));
        Assert.Throws<InvalidOperationException>(() => attempt.MarkVerificationUnknown("90001", "UNKNOWN", Now));
    }
    [Fact]
    public void HistoryExcludesUnsafeCodesAndCallbackTimestampIsFirstReceipt()
    {
        var attempt = Attempt(); attempt.RecordCallback(Now); attempt.RecordCallback(Now.AddSeconds(10));
        Assert.Equal(Now, attempt.CallbackReceivedAtUtc);
        var history = PaymentAttemptEvent.Create(attempt.Id, PaymentAttemptEventType.CallbackReceived, Now,
            errorCode: "raw message / private", correlationId: "Authorization secret");
        Assert.Null(history.ErrorCode); Assert.Null(history.CorrelationId);
    }
    private static PaymentAttempt Attempt()
    {
        var plan = Plan.Create("payment-test", "آزمون پرداخت", Now);
        var version = PlanVersion.CreateDraft(plan.Id, 1, Now);
        version.SetMonthlyTerms(BillingCalendar.Gregorian, "unit-test-terms", null, null);
        version.SetEntitlement(EntitlementKey.Messaging, false); version.SetPrice(1000, CurrencyCode.Irr); version.Publish(Now);
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), version, Guid.NewGuid(), new byte[32], Now);
        return PaymentAttempt.Create(order, "Zibal", Guid.NewGuid(), new byte[32], Now);
    }
}
