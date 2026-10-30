using System.Diagnostics;
using System.Text.Json;
using MakanApp.Application.Outbox;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Realtime;

internal sealed record InvalidationEnvelope(string Key, Guid? OrganizationId = null, Guid? ClassId = null,
    Guid? ResourceId = null, Guid? TargetUserId = null, bool StaffOnly = false, string? TraceId = null,
    Guid? LearnerOrganizationPersonId = null);

public sealed partial class RealtimeInvalidationWriter(MakanDbContext db, IOutbox outbox, TimeProvider clock)
{
    public const string MessageType = "platform.realtime-invalidation.v1";

    // فقط Storeهای مشخص این متد را پیش از Save موجود فراخوانی می‌کنند؛ hook عمومی SaveChanges نیست.
    public async Task CaptureSchoolAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var pending = new HashSet<InvalidationEnvelope>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            switch (entry.Entity)
            {
                case MakanApp.Domain.Academic.Class item:
                    pending.Add(new("school.class", item.OrganizationId, item.Id, item.Id));
                    pending.Add(new("commerce", item.OrganizationId));
                    if (item.Status != ClassStatus.Active || item.EndedAtUtc.HasValue)
                    {
                        var affected = await MakanApp.Infrastructure.Messaging.MessengerRelationshipQuery.ClassMembers(db)
                            .Where(m => m.OrganizationId == item.OrganizationId && m.ClassId == item.Id).Select(m => m.UserId).Distinct().ToArrayAsync(ct);
                        foreach (var userId in affected) pending.Add(new("access", TargetUserId: userId));
                    }
                    break;
                case AcademicPeriod item:
                    pending.Add(new("school.directory", item.OrganizationId)); break;
                case Course item:
                    pending.Add(new("school.directory", item.OrganizationId)); break;
                case Enrollment item:
                    pending.Add(new("school.roster", item.OrganizationId, item.ClassId, item.ClassId));
                    pending.Add(new("commerce", item.OrganizationId));
                    var learnerUser = await (from person in db.OrganizationPersons join user in db.Users on person.PersonId equals user.PersonId
                        where person.Id == item.LearnerOrganizationPersonId && person.OrganizationId == item.OrganizationId select (Guid?)user.Id).SingleOrDefaultAsync(ct);
                    if (learnerUser.HasValue) pending.Add(new("access", TargetUserId: learnerUser));
                    break;
                case TeacherAssignment item:
                    pending.Add(new("school.roster", item.OrganizationId, item.ClassId, item.ClassId));
                    var teacherUser = await db.Memberships.Where(m => m.Id == item.TeacherMembershipId && m.OrganizationId == item.OrganizationId).Select(m => (Guid?)m.UserId).SingleOrDefaultAsync(ct);
                    if (teacherUser.HasValue) pending.Add(new("access", TargetUserId: teacherUser));
                    break;
                case ClassLesson item:
                    pending.Add(new("school.lesson", item.OrganizationId, item.ClassId, item.Id, StaffOnly: item.Status != LessonStatus.Published)); break;
                case Session item:
                    pending.Add(new("school.schedule", item.OrganizationId, item.ClassId, item.Id)); break;
                case Membership item:
                    pending.Add(new("school.directory", item.OrganizationId));
                    pending.Add(new("access", TargetUserId: item.UserId)); break;
                case RoleAssignment item:
                    var membership = db.Memberships.Local.SingleOrDefault(m => m.Id == item.MembershipId)
                        ?? await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == item.MembershipId, ct);
                    if (membership is not null)
                    {
                        pending.Add(new("school.directory", membership.OrganizationId));
                        pending.Add(new("access", TargetUserId: membership.UserId));
                    }
                    break;
                case OrganizationPerson item:
                    pending.Add(new("school.directory", item.OrganizationId)); break;
                case GuardianRelation item:
                    pending.Add(new("school.guardians", item.OrganizationId));
                    pending.Add(new("access", TargetUserId: item.GuardianUserId)); break;
                case UserSession item when entry.State == EntityState.Modified &&
                    entry.Properties.Any(p => p.IsModified && p.Metadata.Name is nameof(UserSession.SelectedMembershipId) or nameof(UserSession.SelectedRole) or nameof(UserSession.SelectedSubjectOrganizationPersonId)):
                    pending.Add(new("access", TargetUserId: item.UserId)); break;
            }
        }
        Enqueue(pending);
    }

    public void CaptureCommerce()
    {
        db.ChangeTracker.DetectChanges();
        var pending = new HashSet<InvalidationEnvelope>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray())
        {
            if (entry.Entity is Subscription subscription)
                pending.Add(Commerce(subscription.OwnerType, subscription.OwnerId));
            if (entry.Entity is UsageReservation reservation)
                pending.Add(Commerce(reservation.OwnerType, reservation.OwnerId));
            if (entry.Entity is UsageLedgerEntry ledger)
                pending.Add(Commerce(ledger.OwnerType, ledger.OwnerId));
        }
        Enqueue(pending);
    }

    public void CaptureMessaging()
    {
        db.ChangeTracker.DetectChanges();
        var pending = new HashSet<InvalidationEnvelope>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            var owner = entry.Entity switch
            {
                UserContact contact => (Guid?)contact.OwnerUserId,
                MessagingPrivacySettings privacy => privacy.UserId,
                UserBlock block => block.BlockerUserId,
                ConversationParticipant participant when entry.State != EntityState.Added &&
                    entry.Properties.Any(p => p.IsModified && p.Metadata.Name is nameof(ConversationParticipant.Status) or nameof(ConversationParticipant.EndedAtUtc)) => participant.UserId,
                _ => null
            };
            // دلیل Block و هویت طرف مقابل هرگز به طرف دیگر broadcast نمی‌شود.
            if (owner.HasValue) pending.Add(new("access", TargetUserId: owner));
        }
        Enqueue(pending);
    }

    private static InvalidationEnvelope Commerce(SubscriptionOwnerType type, Guid ownerId) =>
        type == SubscriptionOwnerType.Individual ? new("commerce", TargetUserId: ownerId) : new("commerce", OrganizationId: ownerId);

    public void CaptureStorage()
    {
        db.ChangeTracker.DetectChanges();
        var organizations = new HashSet<Guid>();
        foreach (var entry in db.ChangeTracker.Entries<FileAsset>().Where(e => e.Entity.OrganizationId.HasValue))
        {
            var status = entry.Property(f => f.Status);
            if (entry.State == EntityState.Added && entry.Entity.Status == FileAssetStatus.Ready ||
                entry.State == EntityState.Deleted && entry.Entity.Status == FileAssetStatus.Ready ||
                entry.State == EntityState.Modified && status.IsModified && status.OriginalValue != status.CurrentValue &&
                (status.OriginalValue == FileAssetStatus.Ready || status.CurrentValue == FileAssetStatus.Ready))
                organizations.Add(entry.Entity.OrganizationId!.Value);
        }
        // فقط meter واقعی سازمان؛ سهمیهٔ Storage فردیِ پیاده‌سازی‌نشده از روی uploader حدس زده نمی‌شود.
        Enqueue(organizations.Select(id => new InvalidationEnvelope("commerce", id)));
    }

    public async Task CapturePersonalClassesAsync(CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var pending = new HashSet<InvalidationEnvelope>();
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray())
        {
            Guid? classId = entry.Entity switch
            {
                PersonalClass classroom => classroom.Id,
                PersonalClassParticipant participant => participant.PersonalClassId,
                PersonalClassInvitation invitation => invitation.PersonalClassId,
                _ => null
            };
            if (!classId.HasValue) continue;
            var entity = db.PersonalClasses.Local.SingleOrDefault(c => c.Id == classId)
                ?? await db.PersonalClasses.AsNoTracking().SingleAsync(c => c.Id == classId, ct);
            pending.Add(Commerce(SubscriptionOwnerType.Individual, entity.HostUserId));
            pending.Add(new("access", TargetUserId: entity.HostUserId));
            if (entry.Entity is PersonalClassParticipant member) pending.Add(new("access", TargetUserId: member.StudentUserId));
            if (entry.Entity is PersonalClassInvitation invite) pending.Add(new("access", TargetUserId: invite.DestinationUserId));
            if (entry.Entity is PersonalClass { ArchivedAtUtc: not null })
            {
                var learners = await db.PersonalClassParticipants.Where(p => p.PersonalClassId == classId && p.EndedAtUtc == null).Select(p => p.StudentUserId).Distinct().ToArrayAsync(ct);
                foreach (var learner in learners) pending.Add(new("access", TargetUserId: learner));
            }
        }
        Enqueue(pending);
    }

    private void Enqueue(IEnumerable<InvalidationEnvelope> pending)
    {
        foreach (var item in pending)
            outbox.Enqueue(new OutboxEnvelope(Guid.NewGuid(), MessageType,
                JsonSerializer.Serialize(item with { TraceId = Activity.Current?.TraceId.ToString() }), clock.GetUtcNow().UtcDateTime));
    }
}
