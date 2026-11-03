using MakanApp.Domain.Academic;
using Xunit;

namespace MakanApp.UnitTests.Academic;

public sealed class PersonalClassRequestTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SubmissionIsAnIntentAndDoesNotCreateClassOrAssignManager()
    {
        var request = Create();
        Assert.Equal(PersonalClassRequestStatus.Draft, request.Status);
        Assert.True(request.Submit(Now));
        Assert.False(request.Submit(Now.AddMinutes(1)));
        Assert.Null(request.CreatedClassId);
        Assert.Null(request.FinalManagerUserId);
        Assert.Equal(Now, request.SubmittedAtUtc);
    }

    [Fact]
    public void NeedsChangesCanBeEditedAndResubmittedWithStableRequestIdentity()
    {
        var request = Create();
        var id = request.Id;
        var hash = request.CreationPayloadHash;
        request.Submit(Now);
        request.BeginReview(Now.AddMinutes(1));
        request.Decide(PersonalClassRequestStatus.NeedsChanges, Guid.NewGuid(), "توضیح فعالیت تکمیل شود", Now.AddMinutes(2));
        Assert.True(request.CanEdit);
        request.Edit("عنوان اصلاح‌شده", "هدف روشن", null, 20, null, null, Now.AddMinutes(3));
        request.Submit(Now.AddMinutes(4));
        Assert.Equal(id, request.Id);
        Assert.Equal(hash, request.CreationPayloadHash);
        Assert.Equal(PersonalClassRequestStatus.Submitted, request.Status);
        Assert.Null(request.DecisionReason);
    }

    [Fact]
    public void ApprovalRequiresSubmittedRequestAndExplicitClassAndManagerBinding()
    {
        var request = Create();
        var reviewer = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => request.Decide(PersonalClassRequestStatus.Approved, reviewer, "تأیید", Now));
        request.Submit(Now);
        Assert.Throws<ArgumentException>(() => request.Decide(PersonalClassRequestStatus.Approved, reviewer, "تأیید", Now));
        var classId = Guid.NewGuid();
        request.Decide(PersonalClassRequestStatus.Approved, reviewer, "تأیید", Now, classId, request.RequesterUserId);
        Assert.Equal(classId, request.CreatedClassId);
        Assert.Equal(request.RequesterUserId, request.FinalManagerUserId);
        Assert.False(request.CanEdit);
        Assert.False(request.CanCancel);
        Assert.Throws<InvalidOperationException>(() => request.Cancel(Now));
    }

    [Fact]
    public void RejectionAndCancellationNeverCreateClass()
    {
        var rejected = Create();
        rejected.Submit(Now);
        rejected.Decide(PersonalClassRequestStatus.Rejected, Guid.NewGuid(), "خارج از دامنه", Now);
        Assert.Null(rejected.CreatedClassId);
        Assert.Throws<InvalidOperationException>(() => rejected.Submit(Now));
        var cancelled = Create();
        Assert.True(cancelled.Cancel(Now));
        Assert.False(cancelled.Cancel(Now.AddMinutes(1)));
        Assert.Null(cancelled.CreatedClassId);
        Assert.Throws<InvalidOperationException>(() => cancelled.BeginReview(Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ProposedCapacityMustBePositive(int capacity) =>
        Assert.Throws<ArgumentException>(() => Create(capacity));

    private static PersonalClassRequest Create(int capacity = 10) => PersonalClassRequest.Create(
        Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), "کلاس مستقل", "فعالیت مشترک", null, capacity, null, null, Now);
}
