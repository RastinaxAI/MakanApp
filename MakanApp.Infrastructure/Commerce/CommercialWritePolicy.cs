using MakanApp.Application.Commerce;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Commerce;

internal static class CommercialWritePolicy
{
    public static bool IsRelevant(EntityEntry e) => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
        && (e.Entity.GetType().Namespace is "MakanApp.Domain.Academic" or "MakanApp.Domain.Assessment"
            || e.Entity is Membership or RoleAssignment or GuardianRelation or OrganizationPerson or Invitation
            || e.Entity is Person && e.State == EntityState.Modified)
        && e.Entity is not (PersonalClass or PersonalClassParticipant or PersonalClassInvitation);

    public static async Task EnsureAsync(MakanDbContext db, DateTime now, CancellationToken ct)
    {
        var entries = db.ChangeTracker.Entries().Where(IsRelevant).ToArray();
        var groups = new Dictionary<Guid, List<EntityEntry>>();
        foreach (var e in entries)
        {
            if (e.Entity is Person person)
            {
                if (await db.Users.AsNoTracking().AnyAsync(u => u.PersonId == person.Id, ct)) continue;
                foreach (var id in await db.OrganizationPersons.AsNoTracking().Where(p => p.PersonId == person.Id).Select(p => p.OrganizationId).Distinct().ToArrayAsync(ct))
                {
                    if (!groups.TryGetValue(id, out var personChanges)) groups[id] = personChanges = [];
                    personChanges.Add(e);
                }
                continue;
            }
            var property = e.Metadata.FindProperty("OrganizationId");
            Guid? organizationId = property is null ? null : e.Property("OrganizationId").CurrentValue as Guid?;
            if (e.Entity is RoleAssignment role)
                organizationId = db.ChangeTracker.Entries<Membership>().FirstOrDefault(m => m.Entity.Id == role.MembershipId)?.Entity.OrganizationId
                    ?? await db.Memberships.Where(m => m.Id == role.MembershipId).Select(m => (Guid?)m.OrganizationId).SingleOrDefaultAsync(ct);
            if (!organizationId.HasValue) continue;
            if (!groups.TryGetValue(organizationId.Value, out var list)) groups[organizationId.Value] = list = [];
            list.Add(e);
        }
        foreach (var (organizationId, changes) in groups.OrderBy(g => g.Key))
        {
            // قفل مشترک با رزرو و ثبت قرارداد، بررسی ظرفیت و write را در همان transaction نگه می‌دارد.
            await db.Organizations.FromSqlInterpolated($"SELECT * FROM [organization].[Organizations] WITH (UPDLOCK,HOLDLOCK) WHERE [Id]={organizationId}")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (!await db.Subscriptions.AsNoTracking().AnyAsync(s => s.OrganizationId == organizationId, ct)) continue;
            var contracts = await db.Subscriptions.AsNoTracking().Where(s => s.OrganizationId == organizationId && s.CommercialControlEnabled
                && s.StartsAtUtc <= now).OrderByDescending(s => s.StartsAtUtc).ThenByDescending(s => s.CreatedAtUtc).ToArrayAsync(ct);
            var contract = contracts.FirstOrDefault(s => s.IsEffectiveAt(now)) ?? contracts.FirstOrDefault();
            if (contract is null) continue;
            foreach (var entry in changes)
            {
                var reduction = IsReduction(entry);
                var newCapacity = entry.State == EntityState.Added && entry.Entity is AcademicClass or AcademicPeriod or Course or Enrollment or TeacherAssignment or Membership or RoleAssignment or Invitation;
                var action = reduction ? CommercialEducationalAction.AccessReduction : newCapacity ? CommercialEducationalAction.NewCapacity : CommercialEducationalAction.ExistingWrite;
                if (!OrganizationCommercialAccess.IsAllowed(contract, action, now)) throw EfCommercialStore.Error(CommerceErrorCodes.CommercialLocked);
            }
            if (!contract.IsEffectiveAt(now) && !contract.IsInOrganizationGrace(now)) continue;
            var entitlements = await db.PlanEntitlements.AsNoTracking().Where(e => e.PlanVersionId == contract.PlanVersionId && e.Enabled).Select(e => e.Key).ToArrayAsync(ct);
            foreach (var entry in changes.Where(e => !IsReduction(e)))
            {
                var name = entry.Entity.GetType().Name;
                var key = entry.Entity.GetType().Namespace == "MakanApp.Domain.Assessment"
                    ? name.StartsWith("Exam", StringComparison.Ordinal) || name.StartsWith("Question", StringComparison.Ordinal) || name.StartsWith("Answer", StringComparison.Ordinal)
                        ? EntitlementKey.Exams : EntitlementKey.Assignments : EntitlementKey.Academic;
                if (!entitlements.Contains(key)) throw EfCommercialStore.Error(CommerceErrorCodes.FeatureNotEntitled);
            }
            await CapacityAsync(db, organizationId, contract, changes, ct);
        }
    }

    private static bool IsReduction(EntityEntry entry) => entry.State != EntityState.Added && entry.Entity switch
    {
        Membership m => !m.IsActive,
        OrganizationPerson p => !p.IsActive,
        RoleAssignment r => !r.IsActive,
        GuardianRelation r => r.EndedAtUtc.HasValue,
        Enrollment e => e.EndedAtUtc.HasValue,
        TeacherAssignment t => t.EndedAtUtc.HasValue,
        AcademicClass c => c.EndedAtUtc.HasValue,
        Invitation i => i.Status != InvitationStatus.Pending,
        _ => false
    };

    private static async Task CapacityAsync(MakanDbContext db, Guid organizationId, Subscription contract, List<EntityEntry> changes, CancellationToken ct)
    {
        if (!changes.Any(e => e.Entity is AcademicClass or Enrollment)) return;
        var classIds = (await db.Classes.AsNoTracking().Where(c => c.OrganizationId == organizationId && c.Status == ClassStatus.Active && c.EndedAtUtc == null)
            .Select(c => c.Id).ToArrayAsync(ct)).ToHashSet();
        var originalClasses = classIds.Count;
        var originalClassIds = classIds.ToHashSet();
        foreach (var entry in changes.Where(e => e.Entity is AcademicClass))
        {
            var entity = (AcademicClass)entry.Entity;
            classIds.Remove(entity.Id);
            if (entry.State != EntityState.Deleted && entity.Status == ClassStatus.Active && entity.EndedAtUtc is null) classIds.Add(entity.Id);
        }
        var enrollments = (await db.Enrollments.AsNoTracking().Where(e => e.OrganizationId == organizationId && e.Status == EnrollmentStatus.Active && e.EndedAtUtc == null)
            .Select(e => new { e.ClassId, e.LearnerOrganizationPersonId }).ToArrayAsync(ct)).Select(e => (e.ClassId, e.LearnerOrganizationPersonId)).ToHashSet();
        var originalLearners = enrollments.Where(e => originalClassIds.Contains(e.ClassId)).Select(e => e.LearnerOrganizationPersonId).Distinct().LongCount();
        foreach (var entry in changes.Where(e => e.Entity is Enrollment))
        {
            var entity = (Enrollment)entry.Entity;
            enrollments.Remove((entity.ClassId, entity.LearnerOrganizationPersonId));
            if (entry.State != EntityState.Deleted && entity.Status == EnrollmentStatus.Active && entity.EndedAtUtc is null)
                enrollments.Add((entity.ClassId, entity.LearnerOrganizationPersonId));
        }
        var learners = enrollments.Where(e => classIds.Contains(e.ClassId)).Select(e => e.LearnerOrganizationPersonId).Distinct().LongCount();
        var quotas = await db.PlanQuotaDefinitions.AsNoTracking().Where(q => q.PlanVersionId == contract.PlanVersionId
            && (q.Key == QuotaKey.ActiveClasses || q.Key == QuotaKey.ActiveLearners)).ToDictionaryAsync(q => q.Key, q => q.Limit, ct);
        Check(QuotaKey.ActiveClasses, originalClasses, classIds.Count);
        Check(QuotaKey.ActiveLearners, originalLearners, learners);
        void Check(QuotaKey key, long before, long after)
        {
            if (after <= before) return;
            if (!quotas.TryGetValue(key, out var limit)) throw EfCommercialStore.Error(CommerceErrorCodes.QuotaRequired);
            if (limit.HasValue && after > limit) throw EfCommercialStore.Error(CommerceErrorCodes.QuotaExceeded);
        }
    }
}
