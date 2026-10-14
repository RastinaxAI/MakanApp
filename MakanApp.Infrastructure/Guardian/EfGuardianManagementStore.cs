using MakanApp.Application.Guardian;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Guardian;

public sealed class EfGuardianManagementStore(MakanDbContext database) : IGuardianManagementStore
{
    public Task<GuardianRelation?> GetAsync(Guid organizationId, Guid id, bool forUpdate, CancellationToken cancellationToken) =>
        forUpdate
            ? database.GuardianRelations.FromSqlInterpolated($"SELECT * FROM [guardian].[GuardianRelations] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {id}").SingleOrDefaultAsync(cancellationToken)
            : database.GuardianRelations.AsNoTracking().SingleOrDefaultAsync(item => item.OrganizationId == organizationId && item.Id == id, cancellationToken);
    public Task<GuardianRelation?> FindCurrentAsync(Guid organizationId, Guid guardianId, Guid learnerId, CancellationToken cancellationToken) =>
        database.GuardianRelations.Where(item => item.OrganizationId == organizationId &&
            item.GuardianUserId == guardianId && item.LearnerOrganizationPersonId == learnerId &&
            (item.Status == GuardianRelationStatus.Pending || item.Status == GuardianRelationStatus.Active) && item.EndedAtUtc == null)
            .OrderByDescending(item => item.Status == GuardianRelationStatus.Active).ThenBy(item => item.CreatedAtUtc).ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
    public Task<bool> IsActiveParentAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        (from user in database.Users
         join membership in database.Memberships on user.Id equals membership.UserId
         join role in database.RoleAssignments on membership.Id equals role.MembershipId
         where user.Id == userId && user.AccountStatus == UserAccountStatus.Active &&
               membership.OrganizationId == organizationId && membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null &&
               role.Role == OrganizationRole.Parent && role.Status == RoleAssignmentStatus.Active && role.EndedAtUtc == null
         select user.Id).AnyAsync(cancellationToken);
    public async Task<(int Total, IReadOnlyCollection<GuardianRelation> Items)> ListAsync(Guid organizationId, Guid? learnerId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = database.GuardianRelations.AsNoTracking().Where(item => item.OrganizationId == organizationId &&
            (!learnerId.HasValue || item.LearnerOrganizationPersonId == learnerId.Value));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAtUtc).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return (total, rows);
    }
    public void Add(GuardianRelation relation) => database.GuardianRelations.Add(relation);
}
