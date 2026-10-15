using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ApprovedChildSafetyPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData(2008, 10, 15, CommunicationAgeCategory.Adult)]
    [InlineData(2008, 10, 16, CommunicationAgeCategory.Minor)]
    [InlineData(2000, 1, 1, CommunicationAgeCategory.Adult)]
    public void VerifiedBirthdayDeterminesClassification(int year, int month, int day, CommunicationAgeCategory expected) =>
        Assert.Equal(expected, CommunicationAgeVerification.Classify(new DateOnly(year, month, day), Now));

    [Fact]
    public void FutureOrUnrealisticBirthDateIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CommunicationAgeVerification.Classify(new DateOnly(2027, 1, 1), Now));
        Assert.Throws<ArgumentException>(() => CommunicationAgeVerification.Classify(new DateOnly(1800, 1, 1), Now));
    }

    [Fact]
    public void ConflictUsesUnknownProtectionWithoutDeletingAccount()
    {
        var user = User.Create(Now);
        Assert.Equal(CommunicationAgeVerificationStatus.Unverified, user.CommunicationAgeVerificationStatus);
        user.SetCommunicationAgeCategory(CommunicationAgeCategory.Adult, Now);
        user.MarkCommunicationAgeConflict(Now);
        Assert.Equal(CommunicationAgeCategory.Unknown, user.CommunicationAgeCategory);
        Assert.Equal(CommunicationAgeVerificationStatus.Conflict, user.CommunicationAgeVerificationStatus);
        Assert.Equal(UserAccountStatus.Active, user.AccountStatus);
    }

    [Theory]
    [InlineData("سوء استفاده جنسی از کودک", ContentSafetyDecision.Blocked, "ChildAbuseRisk")]
    [InlineData("می کشمت", ContentSafetyDecision.Blocked, "ThreatViolence")]
    [InlineData("عکس خصوصی بفرست", ContentSafetyDecision.ReviewRequired, "SexualSafetyGrooming")]
    [InlineData("احمق", ContentSafetyDecision.ReviewRequired, "HarassmentBullying")]
    [InlineData("کد ملی", ContentSafetyDecision.ReviewRequired, "SensitivePersonalData")]
    [InlineData("برنده جایزه شدید", ContentSafetyDecision.ReviewRequired, "SpamScam")]
    [InlineData("https://example.test", ContentSafetyDecision.ReviewRequired, "Other.UnsupportedLink")]
    [InlineData("سلام، تکلیف امروز را دیدم", ContentSafetyDecision.Allowed, "NoRuleMatched")]
    public async Task InternalRulesReturnStableOutcomeAndSafeMetadata(string text, ContentSafetyDecision decision, string reason)
    {
        var provider = new InternalMakanContentSafetyProvider();
        var result = await provider.EvaluateAsync(Request(MessageKind.Text, text), CancellationToken.None);
        Assert.Equal(decision, result.Decision);
        Assert.Equal(reason, result.ReasonCode);
        Assert.Equal(InternalMakanContentSafetyProvider.PolicyVersion, result.PolicyVersion);
        Assert.True(Guid.TryParseExact(result.CorrelationId, "N", out _));
        Assert.All(result.PolicyCodes!, code => Assert.DoesNotContain(text, code, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(MessageKind.Image)]
    [InlineData(MessageKind.Video)]
    [InlineData(MessageKind.Voice)]
    [InlineData(MessageKind.File)]
    [InlineData(MessageKind.ContactCard)]
    public async Task UnsupportedMediaNeverSilentlyBecomesAllowed(MessageKind kind)
    {
        var result = await new InternalMakanContentSafetyProvider().EvaluateAsync(Request(kind, "متن بی‌خطر"), CancellationToken.None);
        Assert.Equal(ContentSafetyDecision.ReviewRequired, result.Decision);
        Assert.Equal("Other.UnsupportedMedia", result.ReasonCode);
    }

    [Fact]
    public async Task NormalizationAndPersianDigitsCannotBypassKnownRules()
    {
        var provider = new InternalMakanContentSafetyProvider();
        var threat = await provider.EvaluateAsync(Request(MessageKind.Text, "مي‌كشمت"), CancellationToken.None);
        var phone = await provider.EvaluateAsync(Request(MessageKind.Text, "۰۹۱۲۳۴۵۶۷۸۹"), CancellationToken.None);
        Assert.Equal(ContentSafetyDecision.Blocked, threat.Decision);
        Assert.Equal(ContentSafetyDecision.ReviewRequired, phone.Decision);
    }

    [Fact]
    public void InvitationRequiresExplicitAcceptanceAndCannotAcceptAfterExpiry()
    {
        var invitation = ConversationInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddHours(1), Now);
        Assert.Equal(ConversationInvitationStatus.Pending, invitation.Status);
        Assert.Throws<InvalidOperationException>(() => invitation.Complete(ConversationInvitationStatus.Accepted, Now.AddHours(2)));
        invitation.Complete(ConversationInvitationStatus.Accepted, Now.AddMinutes(1));
        invitation.Complete(ConversationInvitationStatus.Accepted, Now.AddMinutes(2));
        Assert.Equal(ConversationInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(Now.AddMinutes(1), invitation.CompletedAtUtc);
    }

    [Fact]
    public void OfficialClassBindingCannotBeChangedOrAppliedToPersonalConversation()
    {
        var official = Conversation.CreateSystemManagedAcademic(ConversationType.Group, Guid.NewGuid(), "کلاس", null, Now);
        official.BindAcademicClass(Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => official.BindAcademicClass(Guid.NewGuid()));
        var personal = Conversation.CreateDirect(ConversationScope.Personal, null, Guid.NewGuid(), Guid.NewGuid(), Now);
        Assert.Throws<InvalidOperationException>(() => personal.BindAcademicClass(Guid.NewGuid()));
    }

    private static ContentSafetyRequest Request(MessageKind kind, string text) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), kind, text, []);
}
