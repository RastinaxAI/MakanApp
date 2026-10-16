using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ScheduledMessageTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static ScheduledMessage Create(DateTime? due = null) => ScheduledMessage.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new(WorkspaceType.Personal, null, null, null, null), new(MessageKind.Text, "پیام", [], null, [], null, null, false), new byte[32], due ?? Now.AddMinutes(1), Now);
    [Fact]
    public void ScheduleRejectsPastAndNonUtcDeadline()
    {
        Assert.Throws<ArgumentException>(() => Create(Now));
        Assert.Throws<ArgumentException>(() => Create(DateTime.SpecifyKind(Now.AddMinutes(1), DateTimeKind.Unspecified)));
    }
    [Fact]
    public void ClaimRequiresDueTimeAndLeaseAndDispatchHasOneTerminalEffect()
    {
        var row = Create(); var token = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => row.Claim(token, Now, Now.AddSeconds(30)));
        row.Claim(token, Now.AddMinutes(1), Now.AddMinutes(2));
        Assert.Throws<InvalidOperationException>(() => row.Claim(Guid.NewGuid(), Now.AddMinutes(1), Now.AddMinutes(2)));
        row.Dispatch(token, Guid.NewGuid(), Now.AddMinutes(1)); Assert.Equal(ScheduledMessageStatus.Dispatched, row.Status);
        Assert.Throws<InvalidOperationException>(() => row.Cancel(Now.AddMinutes(1)));
    }
    [Fact]
    public void ExpiredWorkerCannotDispatchAfterNewClaimOrCancellation()
    {
        var row = Create(); var old = Guid.NewGuid(); var current = Guid.NewGuid();
        row.Claim(old, Now.AddMinutes(1), Now.AddMinutes(2)); row.Claim(current, Now.AddMinutes(3), Now.AddMinutes(4));
        Assert.Throws<InvalidOperationException>(() => row.Dispatch(old, Guid.NewGuid(), Now.AddMinutes(3)));
        row.Cancel(Now.AddMinutes(3)); row.Cancel(Now.AddMinutes(3));
        Assert.Throws<InvalidOperationException>(() => row.Dispatch(current, Guid.NewGuid(), Now.AddMinutes(3)));
    }
    [Fact]
    public void TransientFailureUsesBoundedBackoffAndMaximumAttempts()
    {
        var row = Create(); var instant = Now.AddMinutes(1);
        for (var attempt = 1; attempt <= ScheduledMessage.MaximumAttempts; attempt++)
        {
            var token = Guid.NewGuid(); row.Claim(token, instant, instant.AddSeconds(30)); row.Fail(token, "SAFETY_UNAVAILABLE", true, instant);
            if (attempt < ScheduledMessage.MaximumAttempts) { Assert.Equal(instant.AddSeconds(5 * (1 << (attempt - 1))), row.NextAttemptAtUtc); instant = row.NextAttemptAtUtc!.Value; }
        }
        Assert.Equal(ScheduledMessageStatus.Failed, row.Status); Assert.Null(row.ClaimToken); Assert.Null(row.DispatchedMessageId);
    }
    [Fact]
    public void ExhaustedCrashLeaseCanBeTerminalizedInsteadOfStickingForever()
    {
        var row = Create();
        for (var i = 1; i <= ScheduledMessage.MaximumAttempts; i++) row.Claim(Guid.NewGuid(), Now.AddMinutes(i), Now.AddMinutes(i).AddSeconds(30));
        row.FailExhausted(Now.AddMinutes(6)); Assert.Equal(ScheduledMessageStatus.Failed, row.Status);
    }
    [Fact]
    public void SilentIsAnImmutableSendIntentNotDeletionOrMissingStorage()
    {
        var message = Message.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, MessageKind.Text, "بی‌صدا", 0, null, null, 4000, Now, isSilent: true);
        Assert.True(message.IsSilent); Assert.False(message.IsDeleted); Assert.Equal("بی‌صدا", message.Text);
        message.EditText("ویرایش", 4000, Now.AddMinutes(1)); Assert.True(message.IsSilent);
    }
}
