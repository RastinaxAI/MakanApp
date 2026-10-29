using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests;

public sealed class SchoolCompletionTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
    private static ClassLesson Draft(string? body = "متن درس", params Guid[] files) =>
        ClassLesson.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), " درس اول ", body, 0, files, Now);

    [Fact]
    public void LessonPublicationIsImmutableAndRetryDoesNotRewriteTimestamp()
    {
        var lesson = Draft();
        Assert.Equal("درس اول", lesson.Title);
        Assert.True(lesson.Publish(Now.AddMinutes(1)));
        Assert.False(lesson.Publish(Now.AddMinutes(2)));
        Assert.Equal(Now.AddMinutes(1), lesson.PublishedAtUtc);
        Assert.Equal(2, lesson.RevisionNumber);
        Assert.Throws<InvalidOperationException>(() => lesson.UpdateDraft("جدید", "متن", 1, [], Now));
    }

    [Fact]
    public void EmptyDraftNeedsTextOrAttachmentBeforePublishing()
    {
        var lesson = Draft(null);
        Assert.Throws<ArgumentException>(() => lesson.Publish(Now));
        lesson.UpdateDraft("فایل", null, 1, [Guid.NewGuid()], Now);
        Assert.True(lesson.Publish(Now));
        Assert.Equal(3, lesson.RevisionNumber);
    }

    [Fact]
    public void AttachmentReplacementPreservesOrderWithoutDuplicatingReferences()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var lesson = Draft(null, a, b);
        lesson.UpdateDraft("درس", null, 2, [b, c], Now);
        Assert.Equal(new[] { b, c }, lesson.Attachments.OrderBy(x => x.DisplayOrder).Select(x => x.FileAssetId));
        Assert.All(lesson.Attachments, x => Assert.Equal(lesson.OrganizationId, x.OrganizationId));
        Assert.Throws<ArgumentException>(() => lesson.UpdateDraft("درس", "متن", 0, [b, b], Now));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100001)]
    public void InvalidLessonOrderIsRejected(int order) =>
        Assert.Throws<ArgumentException>(() => Draft().UpdateDraft("عنوان", "متن", order, [], Now));

    [Fact]
    public void LessonRejectsNonUtcAndOversizedContent()
    {
        Assert.Throws<ArgumentException>(() => Draft().Publish(DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => Draft().UpdateDraft("عنوان", new string('x', 50001), 0, [], Now));
        Assert.Throws<ArgumentException>(() => Draft(null, Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray()));
    }

    [Fact]
    public void GuardianInvitationIsDistinctFromPersonLinkAndNeedsAcceptedReceipt()
    {
        var learner = Guid.NewGuid();
        var invitation = Invitation.CreateGuardianLink(Guid.NewGuid(), learner, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddDays(1));
        Assert.Equal(OrganizationRole.Parent, invitation.Role);
        Assert.Equal(learner, invitation.GuardianLearnerOrganizationPersonId);
        Assert.Null(invitation.OrganizationPersonId);
        Assert.Throws<InvalidOperationException>(() => invitation.ConfirmGuardianRelation(Guid.NewGuid()));
        invitation.Accept(Guid.NewGuid(), Guid.NewGuid(), Now);
        var receipt = Guid.NewGuid(); invitation.ConfirmGuardianRelation(receipt);
        invitation.ConfirmGuardianRelation(receipt);
        Assert.Equal(receipt, invitation.AcceptedGuardianRelationId);
        Assert.Throws<InvalidOperationException>(() => invitation.ConfirmGuardianRelation(Guid.NewGuid()));
    }
}
