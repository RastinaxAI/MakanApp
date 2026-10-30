using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class OfficialSpaceDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ConversationType.Direct)]
    [InlineData(ConversationType.Saved)]
    public void OfficialProvisionRejectsNonPairPurposes(ConversationType purpose) =>
        Assert.Throws<ArgumentException>(() => OfficialSpaceProvision.Request(Guid.NewGuid(), null, purpose, Now));

    [Theory]
    [InlineData(ConversationType.Group)]
    [InlineData(ConversationType.Channel)]
    public void OfficialBindingIsServerBackedAndKeepsOrganizationScope(ConversationType type)
    {
        var org = Guid.NewGuid(); var conversation = Conversation.CreateOfficialOrganization(type, org, "فضای رسمی", Now);
        Assert.Equal(ConversationScope.Organization, conversation.Scope); Assert.Equal(org, conversation.OrganizationId);
        Assert.Null(conversation.CreatedByUserId); Assert.Equal(ConversationManagementPolicy.SystemManagedAcademic, conversation.ManagementPolicy);
        Assert.Equal(org, OfficialSpaceMetadata.From(conversation)!.SourceId);
    }

    [Theory]
    [InlineData(ConversationType.Group)]
    [InlineData(ConversationType.Channel)]
    public void ClassGroupAndChannelHaveSameSourceAndIndependentConversationIdentity(ConversationType type)
    {
        var classroom = Guid.NewGuid(); var conversation = Conversation.CreateSystemManagedAcademic(type, Guid.NewGuid(), "فضای کلاس", null, Now);
        conversation.BindAcademicClass(classroom);
        Assert.Equal(classroom, OfficialSpaceMetadata.From(conversation)!.SourceId);
        Assert.Equal(type, OfficialSpaceMetadata.From(conversation)!.Purpose);
        Assert.False(conversation.IsOfficialOrganizationSpace);
    }

    [Fact]
    public void PendingGroupDoesNotMakeChannelReady()
    {
        var org = Guid.NewGuid(); var group = OfficialSpaceProvision.Request(org, null, ConversationType.Group, Now);
        var channel = OfficialSpaceProvision.Request(org, null, ConversationType.Channel, Now);
        group.RecordAttempt(Now); group.Ready(); channel.RecordAttempt(Now); channel.RequireRepair("ConversationUnavailable");
        Assert.Equal(OfficialSpaceState.Ready, group.State); Assert.Equal(OfficialSpaceState.RepairRequired, channel.State);
        Assert.Equal(1, channel.AttemptCount);
    }

    [Fact]
    public void OrganizationBindingCannotBeReboundToClass()
    {
        var conversation = Conversation.CreateOfficialOrganization(ConversationType.Group, Guid.NewGuid(), "گروه رسمی سازمان", Now);
        Assert.Throws<InvalidOperationException>(() => conversation.BindAcademicClass(Guid.NewGuid()));
        Assert.Null(conversation.AcademicClassId);
    }

    [Fact]
    public void DeliveredActivityCannotBeRedirectedOrDuplicatedToAnotherMessage()
    {
        var delivery = AcademicActivityDelivery.Pending(Guid.NewGuid(), ConversationType.Group);
        var conversation = Guid.NewGuid(); var message = Guid.NewGuid();
        delivery.Attempt(Now); delivery.Deliver(conversation, message); delivery.Deliver(conversation, message);
        Assert.Throws<InvalidOperationException>(() => delivery.Deliver(conversation, Guid.NewGuid()));
        Assert.Equal(message, delivery.MessageId);
    }

    [Fact]
    public void SupersededActivityCannotBecomeCurrentAgain()
    {
        var delivery = AcademicActivityDelivery.Pending(Guid.NewGuid(), ConversationType.Channel); delivery.Supersede();
        Assert.Throws<InvalidOperationException>(() => delivery.Deliver(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void SystemEndedParticipantDoesNotPretendAHumanRemovedThem()
    {
        var participant = ConversationParticipant.CreateActive(Guid.NewGuid(), Guid.NewGuid(), Now);
        participant.EndSystemAccess(Now);
        Assert.False(participant.IsActive); Assert.True(participant.EndedBySystem); Assert.Null(participant.EndedByUserId);
        Assert.Equal(ConversationParticipantStatus.Removed, participant.Status);
    }

    [Fact]
    public void UserMessageFactoryCannotCreateSystemOrigin()
    {
        var message = Message.CreateText(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "اعلان ظاهراً رسمی", 4000, Now);
        Assert.Null(message.AcademicActivityPublicationId);
    }

    [Theory]
    [InlineData(AcademicActivityKind.Lesson)]
    [InlineData(AcademicActivityKind.Assignment)]
    [InlineData(AcademicActivityKind.Exam)]
    public void AcademicActivityReferencesExactlyOneTypedSource(AcademicActivityKind kind)
    {
        var publication = AcademicActivityPublication.Create(kind, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "عنوان مجاز", Now);
        Assert.Equal(1, new[] { publication.LessonId, publication.AssignmentVersionId, publication.ExamVersionId }.Count(x => x.HasValue));
        Assert.Equal(kind, publication.Kind);
    }
}
