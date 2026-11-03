using MakanApp.Domain.Academic;
using MakanApp.Infrastructure.Persistence;

namespace MakanApp.Infrastructure.Academic;

internal static class PersonalClassAcademicAccessQuery
{
    internal sealed class GrantRow
    {
        public Guid MembershipId { get; init; }
        public PersonalClassAcademicCapabilities Capabilities { get; init; }
        public Guid GrantId { get; init; }
    }

    // نقش محلی شرط لازم است؛ هیچ نقش مدیر/مدرس به‌تنهایی مجوز آموزشی ایجاد نمی‌کند.
    internal static IQueryable<GrantRow> Effective(MakanDbContext db, Guid classId) =>
        from grant in db.PersonalClassAcademicGrants
        join member in db.PersonalClassParticipants on grant.MembershipId equals member.Id
        join classroom in db.PersonalClasses on grant.PersonalClassId equals classroom.Id
        where grant.PersonalClassId == classId && member.PersonalClassId == classId && member.EndedAtUtc == null && grant.RevokedAtUtc == null &&
            (grant.ParentGrantId == null
                ? (member.LocalRoles & PersonalClassLocalRoles.Manager) != 0 && classroom.ManagerUserId == member.StudentUserId
                : (member.LocalRoles & PersonalClassLocalRoles.Teacher) != 0 &&
                  db.PersonalClassAcademicGrants.Any(parent => parent.Id == grant.ParentGrantId && parent.PersonalClassId == classId &&
                      parent.ParentGrantId == null && parent.RevokedAtUtc == null && (parent.Capabilities & grant.Capabilities) == grant.Capabilities &&
                      db.PersonalClassParticipants.Any(manager => manager.Id == parent.MembershipId && manager.PersonalClassId == classId &&
                          manager.EndedAtUtc == null && (manager.LocalRoles & PersonalClassLocalRoles.Manager) != 0 &&
                          manager.StudentUserId == classroom.ManagerUserId)))
        select new GrantRow { MembershipId = member.Id, Capabilities = grant.Capabilities, GrantId = grant.Id };
}
