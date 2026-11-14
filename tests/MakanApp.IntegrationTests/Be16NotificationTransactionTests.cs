using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Notification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(NotificationType.PersonalClassRequestUpdated)]
    [InlineData(NotificationType.PersonalClassInvitation)]
    [InlineData(NotificationType.PersonalClassJoinRequestUpdated)]
    [InlineData(NotificationType.PersonalClassManagerTransferUpdated)]
    [InlineData(NotificationType.PersonalClassMembershipChanged)]
    [InlineData(NotificationType.PersonalClassStateChanged)]
    public async Task Be16EveryPersonalClassProducerRollsBackWithBusinessMutation(NotificationType type)
    {
        var host = await ActorAsync(); var target = await ActorAsync(); var now = factory.Clock.GetUtcNow().UtcDateTime;
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "تراکنش واقعی اعلان کلاس");
        var request = await SubmittedClassRequestAsync(target, "تراکنش بررسی");
        await factory.DrainNotificationOutboxAsync();
        Guid source; Guid recipient = target.UserId; Guid eventId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Persistence.MakanDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            switch (type)
            {
                case NotificationType.PersonalClassRequestUpdated:
                    var review = await db.PersonalClassRequests.SingleAsync(r => r.Id == request.Id); review.BeginReview(now); source = review.Id; break;
                case NotificationType.PersonalClassInvitation:
                    var invitation = PersonalClassInvitation.Create(c.Id, target.UserId, Guid.NewGuid(), now, now.AddDays(7)); db.Add(invitation); source = invitation.Id; break;
                case NotificationType.PersonalClassJoinRequestUpdated:
                    var join = PersonalClassJoinRequest.Create(c.Id, target.UserId, Guid.NewGuid(), now); db.Add(join); source = join.Id; recipient = host.UserId; break;
                case NotificationType.PersonalClassManagerTransferUpdated:
                    var transfer = PersonalClassManagerTransfer.Create(c.Id, host.UserId, host.UserId, target.UserId, Guid.NewGuid(), "آزمون انتقال", now); db.Add(transfer); source = transfer.Id; break;
                case NotificationType.PersonalClassMembershipChanged:
                    var member = PersonalClassParticipant.Admit(c.Id, target.UserId, PersonalClassLocalRoles.Learner, now); db.Add(member); source = member.Id; break;
                case NotificationType.PersonalClassStateChanged:
                    (await db.PersonalClasses.SingleAsync(k => k.Id == c.Id)).Suspend(); source = c.Id; recipient = host.UserId; break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
            await scope.ServiceProvider.GetRequiredService<ProductNotificationWriter>().CapturePersonalClassesAsync(default);
            await db.SaveChangesAsync();
            var message = Assert.Single(db.OutboxMessages.Local, o => MakanApp.Application.Notification.NotificationOutboxContract.Matches(o.MessageType, type) && o.Payload.Contains(source.ToString()));
            Assert.True(await db.OutboxMessages.AnyAsync(o => o.Id == message.Id));
            var payload = message.Payload;
            var envelope = System.Text.Json.JsonSerializer.Deserialize<MakanApp.Application.Notification.NotificationOutboxPayload>(payload, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
            eventId = envelope.SourceEventId; Assert.Equal(recipient, envelope.Recipient.UserId);
            await tx.RollbackAsync();
        }
        await factory.DrainNotificationOutboxAsync();
        await WithDb(async db =>
        {
            Assert.False(await db.Notifications.AnyAsync(n => n.SourceEventId == eventId));
            Assert.False(await db.OutboxMessages.AnyAsync(o => o.Payload.Contains(eventId.ToString())));
        });
    }

    [Fact]
    public async Task Be16ChangingOrganizationRoleEmitsOneLogicalNotificationAndNoOpOrRollbackDoesNotDuplicate()
    {
        var actor = await ActorAsync(); var org = await factory.CreateOrganizationAsync("تغییر یک نقش و یک اعلان");
        var membership = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Student); var now = factory.Clock.GetUtcNow().UtcDateTime;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<MakanApp.Application.Organization.IOrganizationStore>();
            var db = scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Persistence.MakanDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            (await db.RoleAssignments.SingleAsync(r => r.Id == membership.RoleAssignmentId)).End(now);
            db.RoleAssignments.Add(RoleAssignment.CreateActive(membership.MembershipId, OrganizationRole.Teacher, now));
            await store.SaveChangesAsync(default); await store.SaveChangesAsync(default); await tx.CommitAsync();
        }
        await Be16NoticeAsync(actor.UserId, NotificationType.MembershipChanged, membership.MembershipId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Persistence.MakanDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            (await db.Memberships.SingleAsync(m => m.Id == membership.MembershipId)).End(now);
            await scope.ServiceProvider.GetRequiredService<MakanApp.Application.Organization.IOrganizationStore>().SaveChangesAsync(default);
            await tx.RollbackAsync();
        }
        await Be16NoticeAsync(actor.UserId, NotificationType.MembershipChanged, membership.MembershipId);
        await WithDb(async db => Assert.Equal(MembershipStatus.Active, (await db.Memberships.SingleAsync(m => m.Id == membership.MembershipId)).Status));
    }
}
