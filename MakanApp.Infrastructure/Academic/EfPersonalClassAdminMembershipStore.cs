using MakanApp.Application.Academic;
using MakanApp.Application.Commerce;
using MakanApp.Application.Platform;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfPersonalClassAdminMembershipStore(MakanDbContext db, CommercialActorAccess actors,
    IPlatformPermissionResolver permissions) : IPersonalClassAdminMembershipStore
{
    public async Task<PersonalClassPage<PlatformPersonalClassMembershipDto>> ReadAsync(PersonalClassActor actor,
        Guid classId, PlatformPersonalClassMembershipQuery query, CancellationToken ct)
    {
        await actors.EnsureAsync(actor.UserId, actor.SessionId, ct);
        if (!await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesRead, ct))
            throw new PlatformException(PlatformErrorCodes.AccessDenied, "مجوز جاری خواندن metadata کلاس مستقل لازم است.");
        var classroom = await db.PersonalClasses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == classId, ct)
            ?? throw EfPersonalClassFormationStore.NotFound();
        var canManageGrants = await permissions.HasPermissionAsync(actor.UserId, PlatformPermissionKeys.PersonalClassesAcademicPermissions, ct);
        // هویت فقط در همین context مجاز projection می‌شود؛ شماره، دلیل پایان و محتوای آموزشی خوانده نمی‌شوند.
        var rows = from member in db.PersonalClassParticipants.AsNoTracking()
            join user in db.Users.AsNoTracking() on member.StudentUserId equals user.Id
            join person in db.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where member.PersonalClassId == classId
            select new
            {
                MembershipId = member.Id, UserId = member.StudentUserId, member.LocalRoles,
                member.JoinedAtUtc, member.EndedAtUtc, member.RowVersion,
                DisplayName = person == null ? user.Username ?? "کاربر" : person.DisplayName ?? person.FirstName + " " + person.LastName,
                user.Username
            };
        if (query.ActiveOnly) rows = rows.Where(r => r.EndedAtUtc == null);
        if (query.Role.HasValue) rows = rows.Where(r => (r.LocalRoles & query.Role.Value) != 0);
        if (query.Search is not null)
            rows = rows.Where(r => r.DisplayName.Contains(query.Search) || r.Username != null && r.Username.Contains(query.Search));
        var count = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(r => r.JoinedAtUtc).ThenBy(r => r.MembershipId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(page.Select(r => new PlatformPersonalClassMembershipDto(r.MembershipId, classId,
            r.UserId, r.DisplayName, r.Username, r.LocalRoles,
            r.EndedAtUtc is null ? "Active" : "Ended", DateTime.SpecifyKind(r.JoinedAtUtc, DateTimeKind.Utc),
            r.EndedAtUtc.HasValue ? DateTime.SpecifyKind(r.EndedAtUtc.Value, DateTimeKind.Utc) : null,
            Convert.ToBase64String(r.RowVersion), canManageGrants && r.EndedAtUtc is null &&
            classroom.State != PersonalClassState.Archived && classroom.ManagerUserId == r.UserId &&
            r.LocalRoles.HasFlag(PersonalClassLocalRoles.Manager))).ToArray(), query.Page, query.PageSize, count);
    }
}
