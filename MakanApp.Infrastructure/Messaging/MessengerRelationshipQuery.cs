using MakanApp.Domain.Academic;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

internal static class MessengerRelationshipQuery
{
    internal sealed class ClassMember
    {
        public Guid OrganizationId { get; init; }
        public Guid ClassId { get; init; }
        public Guid UserId { get; init; }
        public bool IsTeacher { get; init; }
    }

    public static IQueryable<ClassMember> ClassMembers(MakanDbContext db)
    {
        var students =
            from enrollment in db.Enrollments
            join person in db.OrganizationPersons on enrollment.LearnerOrganizationPersonId equals person.Id
            join user in db.Users on person.PersonId equals user.PersonId
            join membership in db.Memberships on new { user.Id, enrollment.OrganizationId }
                equals new { Id = membership.UserId, membership.OrganizationId }
            join role in db.RoleAssignments on membership.Id equals role.MembershipId
            join academicClass in db.Classes on enrollment.ClassId equals academicClass.Id
            join organization in db.Organizations on enrollment.OrganizationId equals organization.Id
            where enrollment.Status == EnrollmentStatus.Active && enrollment.EndedAtUtc == null &&
                person.OrganizationId == enrollment.OrganizationId && person.Status == OrganizationPersonStatus.Active &&
                person.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active &&
                membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null &&
                role.Role == OrganizationRole.Student && role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null &&
                academicClass.OrganizationId == enrollment.OrganizationId && academicClass.Status == ClassStatus.Active &&
                organization.Status == OrganizationStatus.Active
            select new ClassMember { OrganizationId = enrollment.OrganizationId, ClassId = enrollment.ClassId, UserId = user.Id, IsTeacher = false };
        var teachers =
            from assignment in db.TeacherAssignments
            join membership in db.Memberships on assignment.TeacherMembershipId equals membership.Id
            join role in db.RoleAssignments on membership.Id equals role.MembershipId
            join user in db.Users on membership.UserId equals user.Id
            join academicClass in db.Classes on assignment.ClassId equals academicClass.Id
            join organization in db.Organizations on assignment.OrganizationId equals organization.Id
            where assignment.Status == TeacherAssignmentStatus.Active && assignment.EndedAtUtc == null &&
                membership.OrganizationId == assignment.OrganizationId && membership.Status == MembershipStatus.Active &&
                membership.EndedAtUtc == null && role.Role == OrganizationRole.Teacher &&
                role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null &&
                user.AccountStatus == UserAccountStatus.Active && academicClass.OrganizationId == assignment.OrganizationId &&
                academicClass.Status == ClassStatus.Active && organization.Status == OrganizationStatus.Active
            select new ClassMember { OrganizationId = assignment.OrganizationId, ClassId = assignment.ClassId, UserId = user.Id, IsTeacher = true };
        return students.Union(teachers);
    }

    public static IQueryable<Guid> RelatedUsers(MakanDbContext db, Guid actorId, DateTime nowUtc, Guid? organizationId = null)
    {
        var members = ClassMembers(db);
        var classmates = from actor in members
            join target in members on new { actor.OrganizationId, actor.ClassId } equals new { target.OrganizationId, target.ClassId }
            where actor.UserId == actorId && target.UserId != actorId &&
                (!organizationId.HasValue || actor.OrganizationId == organizationId)
            select target.UserId;
        var guardianPairs =
            from relation in db.GuardianRelations
            join learner in db.OrganizationPersons on relation.LearnerOrganizationPersonId equals learner.Id
            join child in db.Users on learner.PersonId equals child.PersonId
            join parentMembership in db.Memberships on new { relation.OrganizationId, UserId = relation.GuardianUserId }
                equals new { parentMembership.OrganizationId, parentMembership.UserId }
            join parentRole in db.RoleAssignments on parentMembership.Id equals parentRole.MembershipId
            join organization in db.Organizations on relation.OrganizationId equals organization.Id
            where relation.Status == GuardianRelationStatus.Active && relation.EndedAtUtc == null && relation.ValidFromUtc <= nowUtc &&
                learner.OrganizationId == relation.OrganizationId && learner.Status == OrganizationPersonStatus.Active && learner.EndedAtUtc == null &&
                child.AccountStatus == UserAccountStatus.Active && parentMembership.Status == MembershipStatus.Active && parentMembership.EndedAtUtc == null &&
                parentRole.Role == OrganizationRole.Parent && parentRole.Status == RoleAssignmentStatus.Active && parentRole.EndedAtUtc == null &&
                organization.Status == OrganizationStatus.Active && (!organizationId.HasValue || relation.OrganizationId == organizationId) &&
                (relation.GuardianUserId == actorId || child.Id == actorId)
            select relation.GuardianUserId == actorId ? child.Id : relation.GuardianUserId;
        var related = classmates.Union(guardianPairs);
        if (organizationId.HasValue) return related;
        // مخاطب باید رضایت شخص محافظت‌شده را نمایندگی کند؛ ذخیره یک‌طرفه توسط غریبه کافی نیست.
        var contacts = from target in db.Users
            where target.Id != actorId && target.AccountStatus == UserAccountStatus.Active &&
                (target.CommunicationAgeCategory == CommunicationAgeCategory.Adult &&
                 target.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified ||
                 db.UserContacts.Any(c => c.OwnerUserId == target.Id && c.ContactUserId == actorId)) &&
                (db.Users.Any(a => a.Id == actorId && a.CommunicationAgeCategory == CommunicationAgeCategory.Adult &&
                    a.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified) ||
                 db.UserContacts.Any(c => c.OwnerUserId == actorId && c.ContactUserId == target.Id))
            select target.Id;
        return related.Union(contacts);
    }

    // همان مرز موجودِ پروفایل عمومی؛ کارت مخاطب نباید مسیر کشف تازه‌ای بسازد.
    public static IQueryable<User> DiscoverableAccounts(MakanDbContext db, Guid requesterId, DateTime nowUtc)
    {
        var related = RelatedUsers(db, requesterId, nowUtc);
        var requester = db.Users.Where(u => u.Id == requesterId && u.AccountStatus == UserAccountStatus.Active && u.ProfileCompletedAtUtc != null && u.PersonId != null);
        return db.Users.AsNoTracking().Where(u => u.AccountStatus == UserAccountStatus.Active && requester.Any() &&
            (u.Id == requesterId || u.CommunicationAgeCategory == CommunicationAgeCategory.Adult &&
             u.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified &&
             requester.Any(a => a.CommunicationAgeCategory == CommunicationAgeCategory.Adult && a.CommunicationAgeVerificationStatus == CommunicationAgeVerificationStatus.Verified) || related.Contains(u.Id)));
    }
}
