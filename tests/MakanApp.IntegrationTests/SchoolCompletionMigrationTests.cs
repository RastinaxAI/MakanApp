using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class SchoolCompletionMigrationTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task SchoolMigrationPreservesExistingInvitationsAndAllowsIndependentChildrenWithSqlConstraints()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString()) { InitialCatalog = "MakanApp_School_Migration_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("MakanApp_School_Migration_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261028164838_AddUserProfilePhoto");
            var now = DateTime.UtcNow; var org = Organization.Create("مدرسه قبلی", now); var user = User.Create(now);
            var person = Person.Create("فراگیر قبلی", "آزمون", null, now); person.SetDateOfBirth(new DateOnly(2012, 1, 1), now);
            db.Organizations.Add(org); db.Users.Add(user); db.Persons.Add(person); await db.SaveChangesAsync();
            var children = new[] { OrganizationPerson.CreateActive(org.Id, person.Id, now), OrganizationPerson.CreateActive(org.Id, (await AddPersonAsync(db, now)).Id, now) };
            db.OrganizationPersons.AddRange(children); await db.SaveChangesAsync();
            var oldInvitationId = Guid.NewGuid(); var expiry = now.AddDays(1);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [organization].[Invitations] ([Id],[OrganizationId],[DestinationUserId],[InvitedByUserId],[Role],[Status],[CreatedAtUtc],[ExpiresAtUtc])
                VALUES ({oldInvitationId},{org.Id},{user.Id},{user.Id},3,1,{now},{expiry})
                """);
            await migrator.MigrateAsync(); await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var old = await db.Invitations.SingleAsync(x => x.Id == oldInvitationId); Assert.Null(old.GuardianLearnerOrganizationPersonId);
            Assert.Equal(new DateOnly(2012, 1, 1), (await db.Persons.SingleAsync(x => x.Id == person.Id)).DateOfBirth);
            var first = Invitation.CreateGuardianLink(org.Id, children[0].Id, user.Id, user.Id, Guid.NewGuid(), now, expiry);
            var second = Invitation.CreateGuardianLink(org.Id, children[1].Id, user.Id, user.Id, Guid.NewGuid(), now, expiry);
            db.Invitations.AddRange(first, second); await db.SaveChangesAsync();
            Assert.Equal(3, await db.Invitations.CountAsync());
            var duplicate = Guid.NewGuid(); var operation = Guid.NewGuid();
            var unique = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [organization].[Invitations] ([Id],[OrganizationId],[DestinationUserId],[InvitedByUserId],[Role],[Status],[CreatedAtUtc],[ExpiresAtUtc],[GuardianLearnerOrganizationPersonId],[ClientOperationId])
                VALUES ({duplicate},{org.Id},{user.Id},{user.Id},3,1,{now},{expiry},{children[0].Id},{operation})
                """));
            Assert.Contains(unique.Number, new[] { 2601, 2627 });
            var receipt = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[Invitations] SET [Status]=2 WHERE [Id]={first.Id}")); Assert.Equal(547, receipt.Number);
            var missing = Guid.NewGuid();
            var fk = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [organization].[Invitations] SET [GuardianLearnerOrganizationPersonId]={missing} WHERE [Id]={first.Id}")); Assert.Equal(547, fk.Number);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static async Task<Person> AddPersonAsync(MakanDbContext db, DateTime now)
    {
        var person = Person.Create("فراگیر دوم", "آزمون", null, now); db.Persons.Add(person); await db.SaveChangesAsync(); return person;
    }
}
