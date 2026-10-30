using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.UnitTests.Assessment;

public sealed class ExamAttachmentModelTests
{
    [Theory]
    [InlineData(0, 1024)]
    [InlineData(-1, 1024)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void PolicyRequiresExplicitPositiveLimits(int count, long bytes) =>
        Assert.Throws<ArgumentException>(() => new ExamAnswerAttachmentPolicy(false, count, bytes, ["image/png"]));

    [Theory]
    [InlineData("")]
    [InlineData("image/png,application/pdf")]
    [InlineData("https://host/file")]
    [InlineData("image/png;binary")]
    public void PolicyRejectsMalformedMime(string mime) =>
        Assert.Throws<ArgumentException>(() => new ExamAnswerAttachmentPolicy(false, 1, 1024, [mime]));

    [Fact]
    public void PolicyNormalizesAndOwnsImmutableContentTypeCollection()
    {
        var types = new List<string> { " Image/PNG ", "image/png", "application/pdf" };
        var policy = new ExamAnswerAttachmentPolicy(false, 2, 1024, types);
        types.Clear();
        Assert.Equal(new[] { "application/pdf", "image/png" }, policy.AllowedContentTypes);
    }

    [Fact]
    public void LegacyQuestionHasNoGuessedAttachmentLimits()
    {
        var question = NewQuestion(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(question.AttachmentsAllowed);
        Assert.False(question.AnswerTextRequired);
        Assert.Null(question.AttachmentPolicy);
        Assert.Null(question.MaximumAnswerAttachmentCount);
        Assert.Null(question.MaximumAnswerAttachmentBytes);
        Assert.Null(question.AllowedAnswerContentTypes);
    }

    [Fact]
    public void PublishedPolicyCannotMutateAndNewVersionPreservesOriginalSnapshot()
    {
        var now = DateTime.UtcNow;
        var version = ExamVersion.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), 1, "آزمون", null,
            now.AddMinutes(-1), now.AddHours(1), 30, 1, 10m, ExamRandomizationPolicy.None, Guid.NewGuid(), now);
        var question = NewQuestion(version.OrganizationId, version.Id, new(false, 2, 2048, ["image/png"]));
        version.Publish([question], now);
        Assert.Throws<InvalidOperationException>(() => question.UpdateDraft(version, 1, question.Type, question.Prompt, question.Score, null, now, null, true));
        var next = version.CreateNextDraft(2, Guid.NewGuid(), now);
        var copy = question.CopyTo(next.Id, now);
        Assert.Equal(2, copy.MaximumAnswerAttachmentCount);
        copy.UpdateDraft(next, 1, copy.Type, copy.Prompt, copy.Score, null, now, new(false, 1, 1024, ["application/pdf"]), true);
        Assert.Equal(2, question.MaximumAnswerAttachmentCount);
        Assert.Equal("image/png", question.AllowedAnswerContentTypes);
        Assert.Equal("application/pdf", copy.AllowedAnswerContentTypes);
    }

    [Fact]
    public void AttachmentOnlyRevisionIsAnsweredAndHasImmutableBindingIdentity()
    {
        var now = DateTime.UtcNow;
        var attempt = ExamAttempt.Start(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            1, 1, now.AddMinutes(-1), now.AddHours(1), 30, now);
        var question = NewQuestion(attempt.OrganizationId, attempt.ExamVersionId, new(false, 2, 1024, ["image/png"]));
        var mapping = ExamAttemptQuestion.Create(attempt, question.Id, 1);
        var fileId = Guid.NewGuid();
        var revision = AnswerRevision.CreateDescriptive(attempt, mapping, question, string.Empty, 1, Guid.NewGuid(), new('A', 64), Guid.NewGuid(), null, 1, 1, now, [fileId]);
        Assert.True(revision.HasAnswer);
        var attachment = Assert.Single(revision.Attachments);
        Assert.Equal(revision.Id, attachment.AnswerRevisionId);
        Assert.Equal(attempt.Id, attachment.ExamAttemptId);
        Assert.Equal(mapping.Id, attachment.ExamAttemptQuestionId);
        Assert.Equal(attempt.OrganizationId, attachment.OrganizationId);
        Assert.Equal(fileId, attachment.FileAssetId);
    }

    private static QuestionVersion NewQuestion(Guid org, Guid version, ExamAnswerAttachmentPolicy? policy = null) =>
        QuestionVersion.CreateDraft(org, version, 1, ExamQuestionType.Descriptive, "حل مسئله", 10m, null, DateTime.UtcNow, policy);
}
