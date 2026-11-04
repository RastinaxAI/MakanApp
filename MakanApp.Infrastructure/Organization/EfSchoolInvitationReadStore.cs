using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Organization;

public sealed class EfSchoolInvitationReadStore(MakanDbContext db, ISchoolReadStore authority) : ISchoolInvitationReadStore
{
    public async Task<SchoolPage<SchoolSentInvitationDto>> ReadAsync(AccessContext context, SchoolInvitationQuery query, DateTime now, CancellationToken ct)
    {
        if (!await authority.CanManageOrganizationAsync(context, ct))
            throw new OrganizationException(OrganizationErrorCodes.WorkspaceNotAllowed, "مجوز جاری مدیریت سازمان لازم است.");
        var rows = from invitation in db.Invitations.AsNoTracking()
            join user in db.Users.AsNoTracking() on invitation.DestinationUserId equals user.Id
            join person in db.Persons.AsNoTracking() on user.PersonId equals (Guid?)person.Id into people
            from person in people.DefaultIfEmpty()
            where invitation.OrganizationId == context.OrganizationId
            select new
            {
                Invitation = invitation,
                Kind = invitation.GuardianLearnerOrganizationPersonId != null ? SchoolInvitationType.GuardianLink :
                    invitation.OrganizationPersonId != null ? SchoolInvitationType.PersonLink : SchoolInvitationType.Membership,
                Status = invitation.Status == InvitationStatus.Pending && invitation.ExpiresAtUtc <= now ? InvitationStatus.Expired : invitation.Status,
                DisplayName = person == null ? user.Username ?? "حساب دعوت‌شده" : person.DisplayName ?? person.FirstName + " " + person.LastName,
                user.Username
            };
        if (query.Status.HasValue) rows = rows.Where(r => r.Status == query.Status.Value);
        if (query.InvitationType.HasValue) rows = rows.Where(r => r.Kind == query.InvitationType.Value);
        var total = await rows.CountAsync(ct);
        var page = await rows.OrderByDescending(r => r.Invitation.CreatedAtUtc).ThenBy(r => r.Invitation.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(now, query.Page, query.PageSize, total, page.Select(r => new SchoolSentInvitationDto(
            r.Invitation.Id, r.Kind, r.Invitation.OrganizationId, r.Invitation.DestinationUserId, r.DisplayName, r.Username,
            r.Invitation.OrganizationPersonId, r.Invitation.GuardianLearnerOrganizationPersonId, r.Invitation.Role, r.Status,
            DateTime.SpecifyKind(r.Invitation.CreatedAtUtc, DateTimeKind.Utc), DateTime.SpecifyKind(r.Invitation.ExpiresAtUtc, DateTimeKind.Utc),
            Convert.ToBase64String(r.Invitation.RowVersion),
            r.Status == InvitationStatus.Pending && r.Kind != SchoolInvitationType.Membership ? new[] { "Revoke" } : Array.Empty<string>())).ToArray());
    }
}
