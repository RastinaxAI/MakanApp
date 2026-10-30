using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;

namespace MakanApp.Infrastructure.Messaging;

public enum OfficialAccessReasonKind { ManagerRole = 1, TeacherAssignment = 2, Enrollment = 3 }
public sealed class OfficialAccessReason
{
    public Guid UserId { get; init; }
    public Guid ReasonId { get; init; }
    public OfficialAccessReasonKind Kind { get; init; }
    public bool CanManage { get; init; }
    public bool CanPublishChannel { get; init; }
}

public static class OfficialSpaceAccessQuery
{
    public static IQueryable<OfficialAccessReason> SourceReasons(MakanDbContext db, Guid organizationId, Guid? classId)
    {
        var managers = from membership in db.Memberships
            join role in db.RoleAssignments on membership.Id equals role.MembershipId
            join organization in db.Organizations on membership.OrganizationId equals organization.Id
            join user in db.Users on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId && membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null &&
                role.Role == OrganizationRole.Manager && role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null &&
                organization.Status == OrganizationStatus.Active && user.AccountStatus == UserAccountStatus.Active &&
                (classId == null || db.Classes.Any(c => c.Id == classId && c.OrganizationId == organizationId && c.Status == MakanApp.Domain.Academic.ClassStatus.Active))
            select new OfficialAccessReason { UserId = user.Id, ReasonId = role.Id, Kind = OfficialAccessReasonKind.ManagerRole, CanManage = true, CanPublishChannel = true };
        if (classId is null) return managers;
        var learnersAndTeachers = MessengerRelationshipQuery.ClassMembers(db).Where(m => m.OrganizationId == organizationId && m.ClassId == classId)
            .Select(m => new OfficialAccessReason { UserId = m.UserId, ReasonId = m.ReasonId,
                Kind = m.IsTeacher ? OfficialAccessReasonKind.TeacherAssignment : OfficialAccessReasonKind.Enrollment,
                CanManage = false, CanPublishChannel = m.IsTeacher });
        return managers.Union(learnersAndTeachers);
    }

    public sealed class ConversationGrant
    {
        public Guid ConversationId { get; init; }
        public Guid OrganizationId { get; init; }
        public Guid? ClassId { get; init; }
        public Guid UserId { get; init; }
        public bool CanPublish { get; init; }
    }

    public static IQueryable<ConversationGrant> Conversations(MakanDbContext db)
    {
        var managers = from c in db.Conversations
            join organization in db.Organizations on c.OrganizationId equals organization.Id
            join member in db.Memberships on organization.Id equals member.OrganizationId
            join role in db.RoleAssignments on member.Id equals role.MembershipId
            join user in db.Users on member.UserId equals user.Id
            where c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic &&
                (c.IsOfficialOrganizationSpace || c.AcademicClassId != null) && organization.Status == OrganizationStatus.Active &&
                (c.AcademicClassId == null || db.Classes.Any(k => k.Id == c.AcademicClassId && k.OrganizationId == organization.Id && k.Status == MakanApp.Domain.Academic.ClassStatus.Active)) &&
                member.Status == MembershipStatus.Active && member.EndedAtUtc == null &&
                role.Role == OrganizationRole.Manager && role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active
            select new ConversationGrant { ConversationId = c.Id, OrganizationId = organization.Id, ClassId = c.AcademicClassId, UserId = user.Id, CanPublish = true };
        var members = from c in db.Conversations
            join member in MessengerRelationshipQuery.ClassMembers(db) on new { c.OrganizationId, c.AcademicClassId }
                equals new { OrganizationId = (Guid?)member.OrganizationId, AcademicClassId = (Guid?)member.ClassId }
            where c.ManagementPolicy == ConversationManagementPolicy.SystemManagedAcademic
            select new ConversationGrant { ConversationId = c.Id, OrganizationId = member.OrganizationId, ClassId = member.ClassId,
                UserId = member.UserId, CanPublish = c.Type == ConversationType.Group || member.IsTeacher };
        return managers.Union(members);
    }
}
