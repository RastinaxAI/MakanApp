using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingPolicyRevisionTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<object[]> AgePairs() => from actor in Enum.GetValues<CommunicationAgeCategory>()
        from target in Enum.GetValues<CommunicationAgeCategory>() select new object[] { actor, target };

    [Theory]
    [MemberData(nameof(AgePairs))]
    public void PersonalAuthorizationUsesPrivacyAndAccountsForEveryAgePair(CommunicationAgeCategory actorAge, CommunicationAgeCategory targetAge)
    {
        var actor = Guid.NewGuid();
        var facts = new CommunicationEligibilityFacts(actor, Guid.NewGuid(), ConversationScope.Personal, null,
            new AccessContext(actor, actor, Guid.NewGuid(), WorkspaceType.Personal, null, null, null, null),
            true, true, actorAge, targetAge, true, true, false, false, false, new HashSet<OrganizationRole>(),
            false, false, false, false);
        var policy = new CommunicationEligibilityPolicy();
        Assert.True(policy.CanStartOrSend(facts, MessagingEligibilityOperation.Start));
        Assert.True(policy.CanStartOrSend(facts, MessagingEligibilityOperation.Send));
        Assert.True(policy.CanInviteToManagedConversation(facts));
        Assert.False(policy.CanStartOrSend(facts with { TargetAllowsPersonalDirect = false }, MessagingEligibilityOperation.Start));
        Assert.False(policy.CanInviteToManagedConversation(facts with { TargetAllowsPersonalGroupInvite = false }));
        Assert.False(policy.CanStartOrSend(facts with { TargetAccountAvailable = false }, MessagingEligibilityOperation.Send));
        Assert.False(policy.CanStartOrSend(facts with { ActorAccountAvailable = false }, MessagingEligibilityOperation.Start));
    }

    [Theory]
    [InlineData(ConversationType.Group)]
    [InlineData(ConversationType.Channel)]
    public void OfficialManagementIsRelationshipDerivedWithoutRosterOrOwnershipPowers(ConversationType type)
    {
        var result = ConversationCapabilityPolicy.Evaluate(Facts(type) with { OfficialManager = true, OfficialPublisher = true });
        Assert.True(result.CanEditInfo); Assert.True(result.CanManageMessages); Assert.True(result.CanPublish);
        Assert.False(result.CanManageMembers); Assert.False(result.CanManageAdmins); Assert.False(result.CanChangeSettings);
        Assert.False(result.CanInviteMembers); Assert.False(result.CanManageInvites); Assert.False(result.CanBanMembers);
        Assert.Equal(type == ConversationType.Group, result.CanManageTopics);
        Assert.Equal(ConversationParticipantRole.Member, Facts(type).Role);
    }

    [Theory]
    [InlineData(true, false, true, ConversationStatus.Active)]
    [InlineData(false, true, true, ConversationStatus.Active)]
    [InlineData(false, false, false, ConversationStatus.Active)]
    [InlineData(false, false, true, ConversationStatus.Archived)]
    public void ActualRestrictionsAndRevocationOverrideOfficialManagement(bool restricted, bool banned, bool participant, ConversationStatus status)
    {
        var facts = Facts(ConversationType.Channel) with { OfficialManager = true, OfficialPublisher = true,
            ActorRestricted = restricted, Banned = banned, ActiveParticipant = participant, Status = status };
        Assert.Equal(ConversationEffectiveCapabilities.None, ConversationCapabilityPolicy.Evaluate(facts));
    }

    [Fact]
    public void StudentGroupSendDoesNotGrantOfficialAdministrationOrChannelPublishing()
    {
        var group = ConversationCapabilityPolicy.Evaluate(Facts(ConversationType.Group) with { OfficialPublisher = true });
        var channel = ConversationCapabilityPolicy.Evaluate(Facts(ConversationType.Channel));
        Assert.True(group.CanPublish); Assert.False(group.CanManageMessages); Assert.False(channel.CanPublish);
        Assert.False(group.CanManageMembers); Assert.False(channel.CanManageAdmins);
    }

    [Fact]
    public void UserManagedPermissionsAndMemberRestrictionsRemainEffective()
    {
        var facts = Facts(ConversationType.Group) with { ManagementPolicy = ConversationManagementPolicy.UserManaged,
            Role = ConversationParticipantRole.Admin, AdminPermissions = ConversationAdminPermission.ChangeInfo | ConversationAdminPermission.DeleteMessages,
            MemberCapabilities = GroupMemberCapability.SendMessages, DeniedCapabilities = GroupMemberCapability.SendMessages };
        var result = ConversationCapabilityPolicy.Evaluate(facts);
        Assert.True(result.CanEditInfo); Assert.True(result.CanManageMessages);
        Assert.False(result.CanManageAdmins); Assert.False(result.CanPublish); Assert.False(result.CanInviteMembers);
    }

    [Fact]
    public void CatalogCoversEveryAtomicPermissionAndExplainsOfficialDelegationLimits()
    {
        var catalog = ConversationPermissionCatalog.Get();
        var adminFlags = catalog.Where(item => item.Category == "AdminPermission").Select(item => item.Flag).ToArray();
        Assert.Equal(13, adminFlags.Length); Assert.Equal(3, catalog.Count(item => item.Category == "MemberCapability"));
        Assert.Equal((long)ConversationAdminPermission.All, adminFlags.Aggregate(0L, (value, flag) => value | flag));
        Assert.All(catalog, item => { Assert.True(item.Flag > 0 && (item.Flag & (item.Flag - 1)) == 0); Assert.NotEmpty(item.Description); Assert.NotEmpty(item.DelegationRequirement); });
        Assert.False(catalog.Single(item => item.Key == "ManageMembers").AppliesToSystemManagedAcademic);
        Assert.True(catalog.Single(item => item.Key == "DeleteMessages").OfficialRelationshipDerivedOnly);
        Assert.Equal(ConversationType.Group, Assert.Single(catalog.Single(item => item.Key == "ManageTopics").ConversationTypes));
        Assert.Equal(ConversationType.Channel, Assert.Single(catalog.Single(item => item.Key == "PublishMessages").ConversationTypes));
    }

    [Fact]
    public void InitialInvitationReceiptDoesNotClaimActiveMembershipAndUsesApprovedSevenDays()
    {
        var conversation = Guid.NewGuid(); var target = Guid.NewGuid();
        var invitation = ConversationInvitation.Create(conversation, Guid.NewGuid(), target,
            Now.Add(ConversationInvitation.DefaultInitialLifetime), Now);
        var receipt = ConversationInitialTarget.Create(conversation, target, ConversationInitialTargetOutcome.InvitationPending, invitation);
        Assert.Equal(ConversationInvitationStatus.Pending, invitation.Status);
        Assert.Equal(Now.AddDays(7), receipt.InvitationExpiresAtUtc);
        Assert.Equal(invitation.Id, receipt.InvitationId);
        Assert.Throws<ArgumentException>(() => ConversationInitialTarget.Create(conversation, target, ConversationInitialTargetOutcome.Added, invitation));
        Assert.Throws<ArgumentException>(() => ConversationInitialTarget.Create(conversation, Guid.NewGuid(), ConversationInitialTargetOutcome.InvitationPending, invitation));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void InitialInvitationAcceptanceUsesStoredExpirationBoundary(long offsetTicks, bool allowed)
    {
        var invitation = ConversationInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Now.Add(ConversationInvitation.DefaultInitialLifetime), Now);
        var attemptedAt = invitation.ExpiresAtUtc.AddTicks(offsetTicks);
        if (allowed)
        {
            invitation.Complete(ConversationInvitationStatus.Accepted, attemptedAt);
            Assert.Equal(ConversationInvitationStatus.Accepted, invitation.Status);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => invitation.Complete(ConversationInvitationStatus.Accepted, attemptedAt));
            Assert.Equal(ConversationInvitationStatus.Pending, invitation.Status);
            Assert.Null(invitation.CompletedAtUtc);
        }
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAtUtc);
    }

    [Fact]
    public void InitialInvitationDefaultDoesNotRewriteExplicitStoredLifetime()
    {
        var invitation = ConversationInvitation.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(30), Now);
        invitation.Complete(ConversationInvitationStatus.Accepted, Now.AddDays(8));
        Assert.Equal(Now.AddDays(30), invitation.ExpiresAtUtc);
        Assert.Equal(ConversationInvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public void OfficialDescriptionAndSourceRenamePreserveSystemOwnershipAndPersonalBoundary()
    {
        var official = Conversation.CreateSystemManagedAcademic(ConversationType.Group, Guid.NewGuid(), "کلاس", null, Now);
        official.UpdateOfficialDescription("توضیح مجاز"); official.SynchronizeOfficialTitle("نام تازهٔ کلاس");
        Assert.Equal(ConversationManagementPolicy.SystemManagedAcademic, official.ManagementPolicy);
        Assert.Null(official.CreatedByUserId); Assert.Null(official.ClientOperationId);
        Assert.Throws<InvalidOperationException>(() => official.UpdateInformation("عنوان دستی", null));
        var personal = Conversation.CreateUserManaged(ConversationType.Group, ConversationScope.Personal, null, "شخصی", null,
            Guid.NewGuid(), Guid.NewGuid(), new byte[32], Now);
        Assert.Throws<InvalidOperationException>(() => personal.SynchronizeOfficialTitle("رسمی"));
    }

    [Fact]
    public void SessionRevisionChangesOnlyForRealLifecycleMutations()
    {
        var session = Session.CreateScheduled(Guid.NewGuid(), Guid.NewGuid(), "جلسه", Now.AddDays(1), Now.AddDays(1).AddHours(1), "UTC", null, Now);
        Assert.Equal(1, session.ActivityRevision);
        session.Update(session.Title, session.StartUtc, session.EndUtc, session.TimeZoneId, session.MeetingUrl, Now);
        Assert.Equal(1, session.ActivityRevision);
        session.Update("نام تازه", session.StartUtc, session.EndUtc, session.TimeZoneId, session.MeetingUrl, Now);
        Assert.Equal(2, session.ActivityRevision);
        session.Cancel(Now); Assert.Equal(3, session.ActivityRevision);
        Assert.Throws<InvalidOperationException>(() => session.Cancel(Now));
    }

    [Fact]
    public void SessionPublicationRequiresExplicitActionAndCannotPretendToBeLesson()
    {
        var organization = Guid.NewGuid(); var classroom = Guid.NewGuid(); var source = Guid.NewGuid(); var actor = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => AcademicActivityPublication.Create(AcademicActivityKind.Session, organization, classroom, source, 1, actor, "جلسه", Now));
        var publication = AcademicActivityPublication.Create(AcademicActivityKind.Session, organization, classroom, source, 1, actor, "جلسه", Now,
            sessionAction: SessionActivityAction.Scheduled);
        Assert.Equal(source, publication.SessionId); Assert.Null(publication.LessonId); Assert.Null(publication.ExamVersionId);
    }

    private static ConversationCapabilityFacts Facts(ConversationType type) => new(type, ConversationManagementPolicy.SystemManagedAcademic,
        ConversationStatus.Active, true, ConversationParticipantRole.Member, ConversationAdminPermission.None,
        GroupMemberCapability.None, false, false, false, false, GroupMemberCapability.None);
}
