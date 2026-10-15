using MakanApp.Application.Messaging;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<CommunicationAgeResult> ReadAgeStateAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<MakanDbContext>().Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return new(user.Id, user.CommunicationAgeCategory, user.CommunicationAgeVerificationStatus, Convert.ToBase64String(user.RowVersion));
    }
    public async Task<int> CountAgeEvidenceAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MakanDbContext>().CommunicationAgeVerifications.CountAsync(v => v.UserId == userId);
    }
    public async Task<string?> ReadSafetyPolicyCodesAsync(Guid incidentId)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MakanDbContext>().SafetyIncidents
            .Where(i => i.Id == incidentId).Select(i => i.PolicyCodes).SingleAsync();
    }
    public async Task MarkLegacyAgeConflictForMfaTestAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.MarkCommunicationAgeConflict(Clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync();
    }
    public async Task RemoveApprovedContactAsync(Guid ownerId, Guid contactId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var rows = await db.UserContacts.Where(c => c.OwnerUserId == ownerId && c.ContactUserId == contactId).ToArrayAsync();
        db.UserContacts.RemoveRange(rows);
        await db.SaveChangesAsync();
    }
}
