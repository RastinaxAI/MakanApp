using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MediaSafetyReviewTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string Hash = new('A', 64);

    [Fact]
    public void PendingCaseCannotBeResolvedOrDismissedIntoAnApproval()
    {
        var review = Review(); var reviewer = Guid.NewGuid(); review.StartReview(reviewer, Now);
        Assert.Throws<InvalidOperationException>(() => review.Resolve(reviewer, null, Now));
        Assert.Throws<InvalidOperationException>(() => review.Dismiss(reviewer, null, Now));
        Assert.Equal(ContentSafetyDecision.ReviewRequired, review.MediaDecision);
        Assert.Equal(ModerationQueueScope.PlatformSafety, review.QueueScope);
        Assert.Null(review.AbuseReportId); Assert.Null(review.SafetyIncidentId);
    }

    [Theory]
    [InlineData(ContentSafetyDecision.Allowed)]
    [InlineData(ContentSafetyDecision.Blocked)]
    public void OnlyAssignedReviewerMakesOneExplicitTerminalContentDecision(ContentSafetyDecision decision)
    {
        var review = Review(); var reviewer = Guid.NewGuid(); review.StartReview(reviewer, Now);
        Assert.Throws<InvalidOperationException>(() => review.DecideMedia(Guid.NewGuid(), decision, Now));
        review.DecideMedia(reviewer, decision, Now);
        Assert.Equal(decision, review.MediaDecision); Assert.Equal(reviewer, review.MediaReviewedByUserId);
        Assert.Equal(Now, review.MediaReviewedAtUtc);
        Assert.Throws<InvalidOperationException>(() => review.DecideMedia(reviewer, ContentSafetyDecision.Allowed, Now));
        Assert.Throws<InvalidOperationException>(() => review.Escalate(reviewer, Now));
        review.Resolve(reviewer, "تصمیم صریح ثبت شد", Now);
        Assert.Equal(ModerationCaseStatus.Resolved, review.Status);
    }

    [Fact]
    public void ApprovalMatchesExactFileContentAndCannotApproveAnotherBlob()
    {
        var review = Review(); var reviewer = Guid.NewGuid(); review.StartReview(reviewer, Now);
        review.DecideMedia(reviewer, ContentSafetyDecision.Allowed, Now);
        Assert.True(review.MatchesMedia(review.MediaFileAssetId!.Value, Hash.ToLowerInvariant(), "application/pdf", 42));
        Assert.False(review.MatchesMedia(Guid.NewGuid(), Hash, "application/pdf", 42));
        Assert.False(review.MatchesMedia(review.MediaFileAssetId.Value, new string('B', 64), "application/pdf", 42));
        Assert.False(review.MatchesMedia(review.MediaFileAssetId.Value, Hash, "image/png", 42));
        Assert.False(review.MatchesMedia(review.MediaFileAssetId.Value, Hash, "application/pdf", 43));
        Assert.Throws<InvalidOperationException>(() => review.Dismiss(reviewer, null, Now));
    }

    [Theory]
    [InlineData("short", 1)]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ", 1)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", 0)]
    public void ReviewRequiresAnActualValidFingerprint(string hash, long size) =>
        Assert.Throws<ArgumentException>(() => ModerationCase.CreateForMediaReview(Guid.NewGuid(), Guid.NewGuid(), hash, "application/pdf", size, Now));

    [Theory]
    [InlineData(ContentSafetyDecision.Allowed)]
    [InlineData(ContentSafetyDecision.Blocked)]
    [InlineData(ContentSafetyDecision.ReviewRequired)]
    [InlineData(ContentSafetyDecision.Unavailable)]
    public async Task MediaDecisionIsActuallyRequiredAndNeverAssumed(ContentSafetyDecision decision)
    {
        var provider = new ReviewedMediaContentSafetyProvider(new(), new MediaProvider(decision));
        var result = await provider.EvaluateAsync(Request(null), default);
        Assert.Equal(decision, result.Decision);
        if (decision != ContentSafetyDecision.Unavailable) Assert.True(result.UsesMediaReview);
    }

    [Fact]
    public async Task ApprovedMediaDoesNotApproveUnsafeCaption()
    {
        var provider = new ReviewedMediaContentSafetyProvider(new(), new MediaProvider(ContentSafetyDecision.Allowed));
        var result = await provider.EvaluateAsync(Request("می کشمت"), default);
        Assert.Equal(ContentSafetyDecision.Blocked, result.Decision);
        Assert.False(result.UsesMediaReview);
    }

    private static ModerationCase Review() => ModerationCase.CreateForMediaReview(Guid.NewGuid(), Guid.NewGuid(), Hash, "application/pdf", 42, Now);
    private static ContentSafetyRequest Request(string? text) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), MessageKind.File,
        text, [new(Guid.NewGuid(), "application/pdf", 42, Hash)]);
    private sealed class MediaProvider(ContentSafetyDecision decision) : IMediaSafetyProvider
    {
        public Task<MediaSafetyResult> EvaluateAsync(ContentSafetyAttachment file, CancellationToken ct) =>
            Task.FromResult(new MediaSafetyResult(decision, Guid.NewGuid(), Now));
    }
}
