using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class SubmissionDeadlinePolicyTests
{
    private static readonly DateTime Deadline = new(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, false, false)]
    [InlineData(0, false, false)]
    [InlineData(1, false, true)]
    [InlineData(-1, true, false)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    public void BoundaryAndLatePermissionUseOneUtcRule(int ticks, bool allowLate, bool rejected)
    {
        var now = Deadline.AddTicks(ticks);
        if (rejected)
            Assert.Throws<SubmissionDeadlinePassedException>(() => SubmissionDeadlinePolicy.EnsureAllowed(now, Deadline, allowLate));
        else
            Assert.Equal(ticks > 0, SubmissionDeadlinePolicy.EnsureAllowed(now, Deadline, allowLate));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LocalClockValuesAreRejectedRatherThanConvertedImplicitly(bool localRequestTime)
    {
        var now = localRequestTime ? DateTime.SpecifyKind(Deadline, DateTimeKind.Local) : Deadline;
        var due = localRequestTime ? Deadline : DateTime.SpecifyKind(Deadline, DateTimeKind.Local);
        Assert.Throws<ArgumentException>(() => SubmissionDeadlinePolicy.EnsureAllowed(now, due, false));
    }

    [Fact]
    public void SqlDatetime2WithoutKindRetainsItsPersistedUtcMeaning()
    {
        var persistedDeadline = DateTime.SpecifyKind(Deadline, DateTimeKind.Unspecified);
        Assert.False(SubmissionDeadlinePolicy.EnsureAllowed(Deadline, persistedDeadline, false));
        Assert.Throws<SubmissionDeadlinePassedException>(() =>
            SubmissionDeadlinePolicy.EnsureAllowed(Deadline.AddTicks(1), persistedDeadline, false));
    }

    [Fact]
    public void ExactDeadlineCanProduceANonLateFinalReceipt()
    {
        var attempt = SubmissionAttempt.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), 1, Deadline.AddHours(-1));
        attempt.SaveAnswer("پاسخ در مرز مهلت", Deadline.AddMinutes(-1));
        attempt.Submit(Deadline, Deadline, false, false);
        Assert.Equal(SubmissionAttemptStatus.Submitted, attempt.Status);
        Assert.Equal(Deadline, attempt.SubmittedAtUtc);
        Assert.False(attempt.IsLate);
    }
}
