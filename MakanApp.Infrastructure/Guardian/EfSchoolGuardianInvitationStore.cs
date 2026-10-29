using MakanApp.Application.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Guardian;

public sealed class EfSchoolGuardianInvitationStore(MakanDbContext db) : ISchoolGuardianInvitationStore
{
    public async Task<Guid?> FindTargetAsync(string? username, string? phone, CancellationToken ct) =>
        await db.Users.Where(u => u.AccountStatus == UserAccountStatus.Active &&
            (username != null && u.NormalizedUsername == username || phone != null && db.UserCredentials.Any(c =>
                c.UserId == u.Id && c.Kind == CredentialKind.MobilePhone && c.NormalizedIdentifier == phone)))
            .Select(u => (Guid?)u.Id).SingleOrDefaultAsync(ct);

    public Task<Invitation?> FindOperationAsync(Guid org, Guid operation, CancellationToken ct) =>
        db.Invitations.FromSqlInterpolated($"SELECT * FROM [organization].[Invitations] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={org} AND [ClientOperationId]={operation}").SingleOrDefaultAsync(ct);
    public Task<Invitation?> FindPendingAsync(Guid org, Guid learner, Guid user, CancellationToken ct) =>
        db.Invitations.FromSqlInterpolated($"SELECT * FROM [organization].[Invitations] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={org} AND [GuardianLearnerOrganizationPersonId]={learner} AND [DestinationUserId]={user} AND [Status]=1").SingleOrDefaultAsync(ct);
    public Task<Invitation?> GetAsync(Guid org, Guid id, CancellationToken ct) =>
        db.Invitations.FromSqlInterpolated($"SELECT * FROM [organization].[Invitations] WITH (UPDLOCK,HOLDLOCK) WHERE [OrganizationId]={org} AND [Id]={id} AND [GuardianLearnerOrganizationPersonId] IS NOT NULL").SingleOrDefaultAsync(ct);
    public void Add(Invitation invitation) => db.Invitations.Add(invitation);
}
