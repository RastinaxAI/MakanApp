using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<int> CountProvisioningRequestsAsync(string clientOperationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.PlatformProvisioningRequests.CountAsync(item =>
            item.ClientOperationId == clientOperationId);
    }

    public async Task<int> CountOrganizationsByNameAsync(string name)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Organizations.CountAsync(item => item.Name == name);
    }

    public async Task<int> CountSubscriptionsAsync(Guid organizationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Subscriptions.CountAsync(item => item.OrganizationId == organizationId);
    }

    public async Task<int> CountPendingManagerInvitationsAsync(Guid organizationId, Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Invitations.CountAsync(item =>
            item.OrganizationId == organizationId && item.DestinationUserId == userId &&
            item.Role == OrganizationRole.Manager && item.Status == InvitationStatus.Pending);
    }

    public async Task<UserAccountStatus> GetUserAccountStatusAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Users.Where(item => item.Id == userId)
            .Select(item => item.AccountStatus)
            .SingleAsync();
    }

    public async Task SuspendUserAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
        user.Suspend(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsSessionActiveAsync(Guid sessionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = Clock.GetUtcNow().UtcDateTime;
        return await dbContext.UserSessions.AnyAsync(item =>
            item.Id == sessionId && item.RevokedAtUtc == null && item.ExpiresAtUtc > nowUtc);
    }

    public async Task<Guid> GetLatestSessionIdAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UserSessions
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Select(item => item.Id)
            .FirstAsync();
    }

    public async Task<IReadOnlyCollection<Guid>> GetSessionIdsAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UserSessions
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => item.Id)
            .ToArrayAsync();
    }

    public async Task<int> CountActiveManagersAsync(Guid organizationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await (
            from membership in dbContext.Memberships
            join role in dbContext.RoleAssignments on membership.Id equals role.MembershipId
            join user in dbContext.Users on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId &&
                  membership.Status == MembershipStatus.Active && membership.EndedAtUtc == null &&
                  role.Role == OrganizationRole.Manager && role.Status == RoleAssignmentStatus.Active &&
                  role.EndedAtUtc == null && user.AccountStatus == UserAccountStatus.Active
            select membership.Id).Distinct().CountAsync();
    }

    public async Task<IReadOnlyCollection<OrganizationRole>> GetActiveRolesAsync(Guid membershipId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.RoleAssignments.Where(item =>
                item.MembershipId == membershipId && item.Status == RoleAssignmentStatus.Active &&
                item.EndedAtUtc == null)
            .Select(item => item.Role)
            .ToArrayAsync();
    }
}
