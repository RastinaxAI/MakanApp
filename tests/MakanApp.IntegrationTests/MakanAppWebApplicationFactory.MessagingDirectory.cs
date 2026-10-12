using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task AllowPersonalGroupInviteAsync(Guid inviterUserId, Guid targetUserId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        if (await dbContext.UserContacts.AnyAsync(
                contact => contact.OwnerUserId == targetUserId &&
                           contact.ContactUserId == inviterUserId))
        {
            return;
        }

        dbContext.UserContacts.Add(UserContact.Create(
            targetUserId,
            inviterUserId,
            null,
            DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountUserContactsAsync(Guid ownerUserId, Guid contactUserId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UserContacts.CountAsync(
            contact => contact.OwnerUserId == ownerUserId &&
                       contact.ContactUserId == contactUserId);
    }

    public async Task<string?> GetPersonDisplayNameAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await (
            from user in dbContext.Users.AsNoTracking()
            join person in dbContext.Persons.AsNoTracking() on user.PersonId equals person.Id
            where user.Id == userId
            select person.DisplayName)
            .SingleAsync();
    }

    public async Task<int> CountMessagingPrivacySettingsAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.MessagingPrivacySettings.CountAsync(
            settings => settings.UserId == userId);
    }

    public async Task<CommunicationAgeCategory> GetCommunicationAgeCategoryAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.CommunicationAgeCategory)
            .SingleAsync();
    }
}
