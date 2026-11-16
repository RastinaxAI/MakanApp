using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Knowledge;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class CommercialSubscriptionEndpointsTests
{
    [Theory]
    [InlineData(SubscriptionOwnerType.Individual)]
    [InlineData(SubscriptionOwnerType.Organization)]
    public async Task Be17FileCapacityUsesOwnerAndReadyAssetsWithoutCountingBindingsOrLedgerTwice(SubscriptionOwnerType type)
    {
        var actor = await ActorAsync(); var uploader = await ActorAsync(); var plan = await Be17PlanAsync(type);
        var org = await factory.CreateOrganizationAsync("حساب مستقل ظرفیت فایل");
        var manager = await factory.CreateMembershipAsync(actor.UserId, org, OrganizationRole.Manager);
        var academic = await factory.CreateAcademicClassAsync(org);
        var organizationLearners = await factory.CreateOrganizationPersonsAsync(org, 1);
        var enrollment = await factory.CreateEnrollmentAsync(org, academic.ClassId, organizationLearners[0]);
        var owner = type == SubscriptionOwnerType.Organization ? SubscriptionOwner.Organization(org) : SubscriptionOwner.Individual(actor.UserId);
        var subscriptionId = await SubscribeAsync(owner, plan.VersionId);
        if (type == SubscriptionOwnerType.Organization) await SelectAsync(actor, manager.MembershipId, OrganizationRole.Manager);
        var now = factory.Clock.GetUtcNow().UtcDateTime;
        await WithDb(async db =>
        {
            var personal = PersonalClass.Create(actor.UserId, "کلاس مالی میزبان", Guid.NewGuid(), now);
            var hostMember = PersonalClassParticipant.Admit(personal.Id, actor.UserId, PersonalClassLocalRoles.Manager, now);
            var learner = PersonalClassParticipant.Admit(personal.Id, uploader.UserId, PersonalClassLocalRoles.Learner, now);
            db.PersonalClasses.Add(personal); db.PersonalClassParticipants.AddRange(hostMember, learner);
            var scope = type == SubscriptionOwnerType.Organization ? AcademicScope.Organization(org) : AcademicScope.Personal(personal.Id);
            var classId = type == SubscriptionOwnerType.Organization ? academic.ClassId : personal.Id;
            var authorId = type == SubscriptionOwnerType.Organization ? manager.MembershipId : hostMember.Id;
            FileAsset File(long size, FileAssetStatus status = FileAssetStatus.Ready)
            {
                var f = FileAsset.CreatePending(Guid.NewGuid(), scope.OrganizationId, uploader.UserId, "fixture.pdf", Guid.NewGuid().ToString("N"), "application/pdf", now, now.AddHours(1));
                if (type == SubscriptionOwnerType.Individual) f.BindPersonalClass(personal.Id, learner.Id);
                if (status != FileAssetStatus.Pending) f.MarkReady(size, new string('a', 64), now);
                if (status == FileAssetStatus.Deleted) f.MarkDeleted(now);
                if (status == FileAssetStatus.Rejected) f.MarkRejected(now);
                db.FileAssets.Add(f); return f;
            }
            var attached = File(100); var retained = File(40); retained.Retain(now);
            File(80, FileAssetStatus.Deleted); File(90, FileAssetStatus.Rejected); File(0, FileAssetStatus.Pending);
            for (var number = 0; number < 2; number++)
            {
                var assignment = Assignment.CreateDraft(scope, classId, authorId, now);
                var version = AssignmentVersion.CreateDraft(scope, classId, assignment.Id, 1, "تکلیف fixture", "پیوست مشترک بدون مصرف دوم", now.AddDays(1), false, 1, 10, authorId, now);
                version.ReplaceAttachments([attached.Id], now);
                version.Publish(now); assignment.Publish(now);
                db.Assignments.Add(assignment); db.AssignmentVersions.Add(version);
                var participation = new AcademicParticipantRef(scope.Kind, type == SubscriptionOwnerType.Organization ? enrollment : learner.Id);
                var recipient = AssignmentRecipient.Create(scope, classId, assignment.Id, version.Id, participation, now);
                var attempt = SubmissionAttempt.CreateDraft(scope, assignment.Id, version.Id, recipient.Id, participation, 1, now);
                attempt.SaveAnswer("پاسخ مصنوعی fixture", now);
                db.AssignmentRecipients.Add(recipient); db.SubmissionAttempts.Add(attempt);
                db.SubmissionAttachments.Add(SubmissionAttachment.Create(scope, attempt.Id, retained.Id, now));
            }
            if (type == SubscriptionOwnerType.Individual)
            {
                var file = FileAsset.CreatePending(Guid.NewGuid(), null, actor.UserId, "personal.pdf", Guid.NewGuid().ToString("N"), "application/pdf", now, now.AddHours(1));
                file.MarkReady(30, new string('b', 64), now); db.FileAssets.Add(file);
                var source = EducationalSource.Create(EducationalSourceScope.Personal, actor.UserId, null, null, Guid.NewGuid(), new string('c', 64), now);
                db.EducationalSources.Add(source);
                db.EducationalSourceVersions.Add(EducationalSourceVersion.Create(source.Id, 1, file.Id, actor.UserId, "منبع شخصی fixture", null, EducationalSourceType.Document, now));
            }
            var alien = FileAsset.CreatePending(Guid.NewGuid(), type == SubscriptionOwnerType.Individual ? org : null,
                uploader.UserId, "outside.pdf", Guid.NewGuid().ToString("N"), "application/pdf", now, now.AddHours(1));
            alien.MarkReady(4_000, new string('d', 64), now); db.FileAssets.Add(alien);
            var subscription = await db.Subscriptions.SingleAsync(s => s.Id == subscriptionId);
            var reservation = UsageReservation.Create(owner, subscription.Id, subscription.PlanVersionId, actor.UserId,
                type == SubscriptionOwnerType.Organization ? CommerceContextType.Organization : CommerceContextType.Personal,
                owner.Id, QuotaKey.StorageBytes, subscription.StartsAtUtc, subscription.EndsAtUtc!.Value, 37, Guid.NewGuid(), new byte[32], false, now);
            reservation.Finalize(UsageReservationStatus.Consumed, "historical_capacity_fixture", now);
            db.UsageReservations.Add(reservation); db.UsageLedgerEntries.Add(UsageLedgerEntry.FromReservation(reservation, now, "historical_capacity_fixture"));
            await db.SaveChangesAsync();
        });
        var uploaderSummary = await GetAsync<CommercialSummaryDto>(uploader.Client, "/api/v1/commerce/personal/subscription");
        Assert.Null(uploaderSummary.SubscriptionId); Assert.Empty(uploaderSummary.Quotas);
        var path = type == SubscriptionOwnerType.Organization ? "organization" : "personal";
        var summary = await GetAsync<CommercialSummaryDto>(actor.Client, $"/api/v1/commerce/{path}/subscription");
        var next = await Be17PlanAsync(type);
        var quote = await PostAsync<CommercialQuoteDto>(actor.Client, $"/api/v1/commerce/{path}/quote",
            new CreateOrderCommand(next.VersionId, Guid.NewGuid(), CommercialOrderPurpose.Upgrade, subscriptionId));
        foreach (var (key, expected) in new[]
        {
            (QuotaKey.StorageBytes, type == SubscriptionOwnerType.Individual ? 170L : 140L),
            (QuotaKey.AssignmentFileBytes, 140L), (QuotaKey.SourceCount, type == SubscriptionOwnerType.Individual ? 1L : 0L)
        })
        {
            var actual = summary.Quotas.Single(q => q.Key == key);
            var preview = quote.EffectiveQuotas.Single(q => q.Key == key);
            Assert.True(actual.IsUsageAvailable); Assert.True(preview.IsUsageAvailable);
            Assert.Equal(expected, actual.Consumed); Assert.Equal(expected, preview.Consumed);
            Assert.Equal(actual.Remaining, preview.Remaining); Assert.Equal(QuotaKind.CurrentCapacity, actual.Kind);
            Assert.Equal(0, actual.Reserved);
        }
    }
}
