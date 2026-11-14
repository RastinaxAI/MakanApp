using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Academic;
using MakanApp.Application.Notification;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Notification;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(PersonalClassRequestStatus.UnderReview)]
    [InlineData(PersonalClassRequestStatus.NeedsChanges)]
    [InlineData(PersonalClassRequestStatus.Rejected)]
    public async Task Be16ClassReviewNotifiesRequesterOnceWithoutCopyingPrivateReview(PersonalClassRequestStatus decision)
    {
        var owner = await ActorAsync(); var other = await ActorAsync(); var admin = await AdminAsync();
        var draft = await PostAsync<PersonalClassRequestDto>(owner.Client, "/api/v1/personal-class-requests",
            new CreatePersonalClassRequestCommand(Guid.NewGuid(), new("آزمون اعلان درخواست", "هدف", null, 5, null, null)));
        await factory.DrainNotificationOutboxAsync();
        await WithDb(async db => Assert.False(await db.Notifications.AnyAsync(n => n.ResourceId == draft.Id)));
        var submitted = await PostAsync<PersonalClassRequestDto>(owner.Client, $"/api/v1/personal-class-requests/{draft.Id}/submit", new PersonalClassVersionCommand(draft.Version));
        const string privateReason = "SYNTHETIC_BE16_PRIVATE_REVIEW_REASON";
        var command = new ReviewPersonalClassRequestCommand(submitted.Version, decision, privateReason);
        await PostAsync<PersonalClassRequestDto>(admin.Client, $"/api/admin/v1/personal-class-requests/{draft.Id}/review", command);
        await PostAsync<PersonalClassRequestDto>(admin.Client, $"/api/admin/v1/personal-class-requests/{draft.Id}/review", command);
        var notice = await Be16NoticeAsync(owner.UserId, NotificationType.PersonalClassRequestUpdated, draft.Id);
        Assert.DoesNotContain(privateReason, notice.Body);
        await WithDb(async db => Assert.False(await db.OutboxMessages.AnyAsync(o => o.MessageType == "notification.personal-class-request-updated.v1" && o.Payload.Contains(privateReason))));
        var target = await GetAsync<NotificationTargetResult>(owner.Client, $"/api/v1/notifications/{notice.Id}/target");
        Assert.Equal($"/api/v1/personal-class-requests/{draft.Id}", target.ApiRoute);
        using var hidden = await other.Client.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Be16ClassInvitationRetryHasOneReceiptAndAcceptanceDoesNotRestoreInvitationPermission()
    {
        var host = await ActorAsync(); var learner = await ActorAsync(); var other = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(host, "اعلان دعوت کلاس");
        var command = new InvitePersonalClassStudentCommand(learner.UserId, Guid.NewGuid());
        var invitation = await PostAsync<PersonalClassInvitationDto>(host.Client, $"/api/v1/personal-classes/{c.Id}/invitations", command);
        var repeated = await PostAsync<PersonalClassInvitationDto>(host.Client, $"/api/v1/personal-classes/{c.Id}/invitations", command);
        Assert.Equal(invitation.Id, repeated.Id); Assert.Equal(invitation.ExpiresAtUtc, repeated.ExpiresAtUtc);
        var notice = await Be16NoticeAsync(learner.UserId, NotificationType.PersonalClassInvitation, invitation.Id);
        Assert.Equal("/api/v1/personal-classes/invitations/page", (await GetAsync<NotificationTargetResult>(learner.Client, $"/api/v1/notifications/{notice.Id}/target")).ApiRoute);
        using var hidden = await other.Client.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var first = await learner.Client.PostAsync($"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null); first.EnsureSuccessStatusCode();
        using var retry = await learner.Client.PostAsync($"/api/v1/personal-classes/invitations/{invitation.Id}/accept", null); retry.EnsureSuccessStatusCode();
        using var stale = await learner.Client.GetAsync($"/api/v1/notifications/{notice.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
        var membership = await WithDb(db => db.PersonalClassParticipants.SingleAsync(m => m.PersonalClassId == c.Id && m.StudentUserId == learner.UserId && m.EndedAtUtc == null));
        await Be16NoticeAsync(learner.UserId, NotificationType.PersonalClassMembershipChanged, membership.Id);
    }

    [Fact]
    public async Task Be16ClassJoinAndDecisionNotifyManagerAndRequesterWithIdempotentRecipients()
    {
        var manager = await ActorAsync(); var learner = await ActorAsync();
        await SubscribeAsync(SubscriptionOwner.Individual(manager.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
        var c = await ApprovedClassAsync(manager, "اعلان پیوستن کلاس");
        using var configured = await manager.Client.PutAsJsonAsync($"/api/v1/personal-classes/{c.Id}/settings", new ConfigurePersonalClassCommand(c.Version, 10, true, "پذیرش صریح"), Json);
        configured.EnsureSuccessStatusCode();
        var command = new CreatePersonalClassJoinCommand(Guid.NewGuid());
        var join = await PostAsync<PersonalClassJoinDto>(learner.Client, $"/api/v1/personal-classes/{c.Id}/join-requests", command);
        Assert.Equal(join.Id, (await PostAsync<PersonalClassJoinDto>(learner.Client, $"/api/v1/personal-classes/{c.Id}/join-requests", command)).Id);
        var managerNotice = await Be16NoticeAsync(manager.UserId, NotificationType.PersonalClassJoinRequestUpdated, join.Id);
        var managerTarget = await GetAsync<NotificationTargetResult>(manager.Client, $"/api/v1/notifications/{managerNotice.Id}/target");
        Assert.Equal($"/api/v1/personal-classes/join-requests?classId={c.Id}", managerTarget.ApiRoute);
        using var listed = await manager.Client.GetAsync(managerTarget.ApiRoute); listed.EnsureSuccessStatusCode();
        var decision = new DecidePersonalClassJoinCommand(join.Version, PersonalClassJoinRequestStatus.Approved, "پذیرش درخواست");
        await PostAsync<PersonalClassJoinDto>(manager.Client, $"/api/v1/personal-classes/join-requests/{join.Id}/decision", decision);
        await PostAsync<PersonalClassJoinDto>(manager.Client, $"/api/v1/personal-classes/join-requests/{join.Id}/decision", decision);
        var learnerNotice = await Be16NoticeAsync(learner.UserId, NotificationType.PersonalClassJoinRequestUpdated, join.Id);
        Assert.Equal("/api/v1/personal-classes/join-requests", (await GetAsync<NotificationTargetResult>(learner.Client, $"/api/v1/notifications/{learnerNotice.Id}/target")).ApiRoute);
    }

    [Fact]
    public async Task Be16ManagerTransferRevokesOldClassNotificationDeliveryAndFinancialHostDoesNotReceiveFutureState()
    {
        var host = await ActorAsync(); var next = await ActorAsync();
        await PostAsync<PushDeviceRegistrationResult>(host.Client, "/api/v1/notifications/devices", new RegisterPushDeviceCommand(PushDevicePlatform.Android, "be16-" + Guid.NewGuid().ToString("N")));
        try
        {
            await SubscribeAsync(SubscriptionOwner.Individual(host.UserId), await PlanAsync(SubscriptionOwnerType.Individual, hosting: true));
            var c = await ApprovedClassAsync(host, "مرز اعلان انتقال مدیر");
            var oldState = await Be16NoticeAsync(host.UserId, NotificationType.PersonalClassStateChanged, c.Id);
            await GetAsync<NotificationTargetResult>(host.Client, $"/api/v1/notifications/{oldState.Id}/target");
            var command = new CreatePersonalClassTransferCommand(c.Version, next.UserId, Guid.NewGuid(), "تحویل مسئولیت");
            var transfer = await PostAsync<PersonalClassTransferDto>(host.Client, $"/api/v1/personal-classes/{c.Id}/manager-transfers", command);
            Assert.Equal(transfer.Id, (await PostAsync<PersonalClassTransferDto>(host.Client, $"/api/v1/personal-classes/{c.Id}/manager-transfers", command)).Id);
            await Be16NoticeAsync(next.UserId, NotificationType.PersonalClassManagerTransferUpdated, transfer.Id);
            var decision = new DecidePersonalClassTransferCommand(transfer.Version, PersonalClassManagerTransferStatus.Accepted);
            await PostAsync<PersonalClassTransferDto>(next.Client, $"/api/v1/personal-classes/manager-transfers/{transfer.Id}/decision", decision);
            await PostAsync<PersonalClassTransferDto>(next.Client, $"/api/v1/personal-classes/manager-transfers/{transfer.Id}/decision", decision);
            await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
            await WithDb(async db => Assert.Equal(2, await db.Notifications.CountAsync(n => n.RecipientUserId == next.UserId && n.Type == NotificationType.PersonalClassManagerTransferUpdated && n.ResourceId == transfer.Id)));
            using var stale = await host.Client.GetAsync($"/api/v1/notifications/{oldState.Id}/target"); Assert.Equal(HttpStatusCode.NotFound, stale.StatusCode);
            var updated = await GetAsync<PersonalClassDto>(next.Client, $"/api/v1/personal-classes/{c.Id}");
            var suspend = new PersonalClassReasonCommand(updated.Version, "توقف کلاس");
            await PostAsync<PersonalClassDto>(next.Client, $"/api/v1/personal-classes/{c.Id}/suspend", suspend);
            await PostAsync<PersonalClassDto>(next.Client, $"/api/v1/personal-classes/{c.Id}/suspend", suspend);
            await Be16NoticeAsync(next.UserId, NotificationType.PersonalClassStateChanged, c.Id);
            await WithDb(async db => Assert.Equal(1, await db.Notifications.CountAsync(n => n.RecipientUserId == host.UserId && n.Type == NotificationType.PersonalClassStateChanged && n.ResourceId == c.Id)));
            factory.NotificationDeliveryProvider.Reset(); await factory.ProcessNotificationDeliveriesAsync();
            var attempt = Assert.Single(await factory.GetDeliveryAttemptsAsync(oldState.Id), a => a.Channel == NotificationDeliveryChannel.Push);
            Assert.Equal(NotificationDeliveryStatus.Suppressed, attempt.Status); Assert.Equal(NotificationDeliveryFailureReason.ResourceAccessRevoked, attempt.FailureReason);
            Assert.DoesNotContain(factory.NotificationDeliveryProvider.PushDeliveries, p => p.NotificationId == oldState.Id);
        }
        finally { await Be16RevokeDevicesAsync(host.UserId); }
    }

    private async Task<MakanApp.Domain.Notification.Notification> Be16NoticeAsync(Guid user, NotificationType type, Guid resource)
    {
        await factory.DrainNotificationOutboxAsync(); await factory.DrainNotificationOutboxAsync();
        return await WithDb(async db => Assert.Single(await db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == user && n.Type == type && n.ResourceId == resource).ToArrayAsync()));
    }

    private Task Be16RevokeDevicesAsync(Guid user) => WithDb(async db =>
    {
        foreach (var device in await db.PushDeviceRegistrations.Where(d => d.UserId == user && d.RevokedAtUtc == null).ToArrayAsync()) device.Revoke(factory.Clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync();
    });
}
