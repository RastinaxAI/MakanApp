using MakanApp.Application.Academic;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Fact]
    [Trait("Category", "MessengerHistory")]
    public async Task PersonalClassOfficialHistoryRemainsAccountBasedAcrossSchoolWorkspaceSelection()
    {
        var host = await ActorAsync(); var learner = await ActorAsync();
        await CompletePersonalMessagingProfile(host); await CompletePersonalMessagingProfile(learner);
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var classroom = await ApprovedClassAsync(host, "تاریخچه فضای رسمی مستقل");
        var invitation = await InviteAsync(host, learner, classroom.Id);
        await PostAsync<PersonalClassDto>(learner.Client, $"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null);
        await ProvisionPersonalClass(classroom.Id);
        var pair = await GetAsync<OfficialSpacesPairResult>(host.Client, $"/api/v1/personal-classes/{classroom.Id}/spaces");
        var group = pair.Group.ConversationId!.Value;
        var sent = await PostAsync<MessageReceiptResult>(host.Client, $"/api/v1/conversations/{group}/messages",
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "پیام پایدار کلاس مستقل" });
        var organization = await factory.CreateOrganizationAsync("مدرسه مستقل از فضای کلاس شخصی");
        var member = await factory.CreateMembershipAsync(learner.UserId, organization, OrganizationRole.Parent);
        await SelectAsync(learner, member.MembershipId, OrganizationRole.Parent);
        var inbox = await GetAsync<AccountInboxPageResult>(learner.Client, "/api/v1/conversations/account");
        Assert.Contains(inbox.Items, c => c.ConversationId == group);
        var history = await GetAsync<ConversationMessagePageResult>(learner.Client, $"/api/v1/conversations/{group}/messages");
        Assert.Contains(history.Messages, m => m.MessageId == sent.MessageId);
        var current = await GetAsync<AccessContext>(learner.Client, "/api/v1/workspaces/current");
        Assert.Equal(member.MembershipId, current.MembershipId);
        await WithDb(async db => {
            Assert.True(await db.Conversations.AnyAsync(
                c => c.Id == group && c.PersonalClassId == classroom.Id && c.OrganizationId == null));
            foreach (var userId in new[] { host.UserId, learner.UserId })
                Assert.True(await db.ConversationParticipants.AsNoTracking().AnyAsync(p => p.ConversationId == group && p.UserId == userId &&
                    p.Status == MakanApp.Domain.Messaging.ConversationParticipantStatus.Active && p.EndedAtUtc == null));
            var stored = await db.Messages.AsNoTracking().SingleAsync(m => m.Id == sent.MessageId);
            Assert.Equal(group, stored.ConversationId); Assert.Equal(host.UserId, stored.SenderUserId);
            Assert.Equal(sent.Sequence, stored.Sequence); Assert.Equal(sent.SentAtUtc, stored.SentAtUtc);
        });
    }
}
