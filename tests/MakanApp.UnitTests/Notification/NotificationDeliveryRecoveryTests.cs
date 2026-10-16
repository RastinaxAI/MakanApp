using MakanApp.Domain.Notification;
using Xunit;

namespace MakanApp.UnitTests.Notification;

public sealed class NotificationDeliveryRecoveryTests
{
    [Fact]
    public void QuietHoursDeferralDoesNotConsumeProviderAttemptBudget()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var attempt = NotificationDeliveryAttempt.Schedule(Guid.NewGuid(), NotificationDeliveryChannel.Push, "destination", now, now);
        var claim = Guid.NewGuid();
        attempt.Claim(claim, now, now.AddSeconds(30));
        attempt.DeferBeforeDelivery(claim, now.AddHours(1));
        Assert.Equal(0, attempt.AttemptCount);
        Assert.Equal(NotificationDeliveryStatus.Deferred, attempt.Status);
        Assert.Null(attempt.ClaimToken);
        Assert.Equal(now.AddHours(1), attempt.NextAttemptAtUtc);
        Assert.Throws<InvalidOperationException>(() => attempt.DeferBeforeDelivery(claim, now.AddHours(2)));
    }

    [Fact]
    public void ExhaustedExpiredClaimBecomesTerminalWithoutClaimingAgain()
    {
        var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
        var attempt = NotificationDeliveryAttempt.Schedule(Guid.NewGuid(), NotificationDeliveryChannel.Push, "destination", now, now);
        attempt.Claim(Guid.NewGuid(), now, now.AddSeconds(1));
        Assert.Throws<InvalidOperationException>(() => attempt.FailExhaustedAbandonedClaim(now, 1));
        Assert.Throws<InvalidOperationException>(() => attempt.FailExhaustedAbandonedClaim(now.AddSeconds(2), 2));
        attempt.FailExhaustedAbandonedClaim(now.AddSeconds(2), 1);
        Assert.Equal(NotificationDeliveryStatus.Failed, attempt.Status);
        Assert.Equal(1, attempt.AttemptCount);
        Assert.Null(attempt.ClaimToken);
        Assert.Null(attempt.NextAttemptAtUtc);
    }
}
