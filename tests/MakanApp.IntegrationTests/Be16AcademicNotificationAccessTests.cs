using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Notification;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Notification;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(NotificationType.AssignmentPublished, "membership")]
    [InlineData(NotificationType.ExamPublished, "membership")]
    [InlineData(NotificationType.AssignmentGradeReleased, "membership")]
    [InlineData(NotificationType.ExamGradeReleased, "membership")]
    [InlineData(NotificationType.AssignmentPublished, "role")]
    [InlineData(NotificationType.ExamPublished, "role")]
    [InlineData(NotificationType.AssignmentGradeReleased, "role")]
    [InlineData(NotificationType.ExamGradeReleased, "role")]
    [InlineData(NotificationType.AssignmentPublished, "enrollment")]
    [InlineData(NotificationType.ExamPublished, "enrollment")]
    [InlineData(NotificationType.AssignmentGradeReleased, "enrollment")]
    [InlineData(NotificationType.ExamGradeReleased, "enrollment")]
    [InlineData(NotificationType.AssignmentPublished, "class")]
    [InlineData(NotificationType.ExamPublished, "class")]
    [InlineData(NotificationType.AssignmentGradeReleased, "class")]
    [InlineData(NotificationType.ExamGradeReleased, "class")]
    public async Task Be16AcademicDeliveryAndTargetRecheckCurrentEnrollmentRoleMembershipAndClass(NotificationType type, string revoke)
    {
        var learner = await ActorAsync(); var org = await factory.CreateOrganizationAsync("دسترسی جاری اعلان آموزشی");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var intent = await WithDb(db => NotificationAcademicFixture.CreateAsync(db, learner.UserId, org, type, now));
        var membership = await WithDb(db => db.Memberships.SingleAsync(m => m.OrganizationId == org && m.UserId == learner.UserId));
        await SelectAsync(learner, membership.Id, OrganizationRole.Student);
        await PostAsync<PushDeviceRegistrationResult>(learner.Client, "/api/v1/notifications/devices", new RegisterPushDeviceCommand(PushDevicePlatform.Android, "be16-" + Guid.NewGuid().ToString("N")));
        try
        {
            await factory.EnqueueNotificationAsync(intent); await factory.DrainNotificationOutboxAsync();
            var allowed = await WithDb(db => db.Notifications.SingleAsync(n => n.SourceEventId == intent.EventId && n.RecipientUserId == learner.UserId));
            var before = await WithDb(db => db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == learner.SessionId));
            await GetAsync<NotificationTargetResult>(learner.Client, $"/api/v1/notifications/{allowed.Id}/target");
            var after = await WithDb(db => db.UserSessions.AsNoTracking().SingleAsync(s => s.Id == learner.SessionId));
            Assert.Equal(before.SelectedMembershipId, after.SelectedMembershipId); Assert.Equal(before.SelectedRole, after.SelectedRole);
            Assert.Equal(before.SelectedSubjectOrganizationPersonId, after.SelectedSubjectOrganizationPersonId);
            factory.NotificationDeliveryProvider.Reset(); await factory.ProcessNotificationDeliveriesAsync();
            Assert.Contains(factory.NotificationDeliveryProvider.PushDeliveries, p => p.NotificationId == allowed.Id);
            var queued = intent with { EventId = Guid.NewGuid() };
            await factory.EnqueueNotificationAsync(queued); await factory.DrainNotificationOutboxAsync();
            var blocked = await WithDb(db => db.Notifications.SingleAsync(n => n.SourceEventId == queued.EventId && n.RecipientUserId == learner.UserId));
            await WithDb(async db =>
            {
                switch (revoke)
                {
                    case "membership": (await db.Memberships.SingleAsync(m => m.Id == membership.Id)).End(now); break;
                    case "role": (await db.RoleAssignments.SingleAsync(r => r.MembershipId == membership.Id && r.Role == OrganizationRole.Student && r.EndedAtUtc == null)).End(now); break;
                    case "enrollment": (await db.Enrollments.SingleAsync(e => e.OrganizationId == org)).Withdraw(now); break;
                    case "class": (await db.Classes.SingleAsync(c => c.OrganizationId == org)).Archive(now); break;
                    default: throw new ArgumentOutOfRangeException(nameof(revoke));
                }
                await db.SaveChangesAsync();
            });
            using var stale = await learner.Client.GetAsync($"/api/v1/notifications/{blocked.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
            factory.NotificationDeliveryProvider.Reset(); await factory.ProcessNotificationDeliveriesAsync();
            var delivery = Assert.Single(await factory.GetDeliveryAttemptsAsync(blocked.Id), a => a.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Suppressed, delivery.Status);
            Assert.Equal(revoke == "membership" ? NotificationDeliveryFailureReason.OrganizationAccessRevoked : NotificationDeliveryFailureReason.ResourceAccessRevoked, delivery.FailureReason);
            Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, p => p.NotificationId == blocked.Id);
            // رسید عمومی تاریخچه حذف نمی‌شود، ولی هیچ مجوز قدیمی را زنده نمی‌کند.
            var history = await GetAsync<NotificationPage>(learner.Client, "/api/v1/notifications"); Assert.Contains(history.Items, n => n.Id == blocked.Id);
        }
        finally { await Be16RevokeDevicesAsync(learner.UserId); }
    }

    [Theory]
    [InlineData(NotificationType.AssignmentGradeReleased)]
    [InlineData(NotificationType.ExamGradeReleased)]
    public async Task Be16GuardianResultDeliveryAndTargetRequireCurrentRelationship(NotificationType type)
    {
        var learner = await ActorAsync(); var parent = await ActorAsync(); var org = await factory.CreateOrganizationAsync("اعلان نتیجهٔ ولی");
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        var source = await WithDb(db => NotificationAcademicFixture.CreateAsync(db, learner.UserId, org, type, now));
        var parentMembership = await factory.CreateMembershipAsync(parent.UserId, org, OrganizationRole.Parent);
        var relation = await WithDb(async db =>
        {
            var person = await db.OrganizationPersons.SingleAsync(p => p.OrganizationId == org);
            var link = GuardianRelation.CreateActive(org, parent.UserId, person.Id, now); db.GuardianRelations.Add(link); await db.SaveChangesAsync(); return link;
        });
        await SelectAsync(parent, parentMembership.MembershipId, OrganizationRole.Parent);
        using var selected = await parent.Client.PostAsync($"/api/v1/guardian/children/{relation.LearnerOrganizationPersonId}/select", null); selected.EnsureSuccessStatusCode();
        await PostAsync<PushDeviceRegistrationResult>(parent.Client, "/api/v1/notifications/devices", new RegisterPushDeviceCommand(PushDevicePlatform.Android, "be16-" + Guid.NewGuid().ToString("N")));
        try
        {
            var intent = source with { Recipients = [new NotificationRecipientIntent(parent.UserId, "نتیجهٔ آموزشی", "نتیجهٔ منتشرشده آماده است.")] };
            await factory.EnqueueNotificationAsync(intent); await factory.DrainNotificationOutboxAsync();
            var notice = await WithDb(db => db.Notifications.SingleAsync(n => n.SourceEventId == intent.EventId && n.RecipientUserId == parent.UserId));
            var target = await GetAsync<NotificationTargetResult>(parent.Client, $"/api/v1/notifications/{notice.Id}/target"); Assert.EndsWith("/guardian-result", target.ApiRoute);
            factory.NotificationDeliveryProvider.Reset(); await factory.ProcessNotificationDeliveriesAsync();
            Assert.Contains(factory.NotificationDeliveryProvider.PushDeliveries, p => p.NotificationId == notice.Id);
            var queued = intent with { EventId = Guid.NewGuid() }; await factory.EnqueueNotificationAsync(queued); await factory.DrainNotificationOutboxAsync();
            var stale = await WithDb(db => db.Notifications.SingleAsync(n => n.SourceEventId == queued.EventId && n.RecipientUserId == parent.UserId));
            await WithDb(async db => { (await db.GuardianRelations.SingleAsync(g => g.Id == relation.Id)).Revoke(now); await db.SaveChangesAsync(); });
            using var denied = await parent.Client.GetAsync($"/api/v1/notifications/{stale.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            factory.NotificationDeliveryProvider.Reset(); await factory.ProcessNotificationDeliveriesAsync();
            Assert.Equal(NotificationDeliveryFailureReason.ResourceAccessRevoked, Assert.Single(await factory.GetDeliveryAttemptsAsync(stale.Id), a => a.Channel == NotificationDeliveryChannel.Push).FailureReason);
            Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, p => p.NotificationId == stale.Id);
        }
        finally { await Be16RevokeDevicesAsync(parent.UserId); }
    }
}
