using MakanApp.Application.Ai;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Ai;

public sealed class EfAiScopeStore(MakanDbContext dbContext) : IAiScopeStore
{
    public Task<Guid?> GetActiveLearnerOrganizationPersonIdAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from organizationPerson in dbContext.OrganizationPersons
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where organizationPerson.OrganizationId == organizationId &&
                  user.Id == userId &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select (Guid?)organizationPerson.Id)
        .SingleOrDefaultAsync(cancellationToken);
}
