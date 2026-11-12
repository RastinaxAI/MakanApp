using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class PersonalClassV2MigrationTests(MakanAppWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersonalClassV2EmptyAndBaselineUpgradePreserveRowsWithoutGuessedCapacity(bool withBaseline)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
        { InitialCatalog = "MakanApp_PersonalClassV2_Migration_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("(localdb)", connection.DataSource, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("MakanApp_PersonalClassV2_Migration_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>().UseSqlServer(connection.ConnectionString).Options);
        Assert.False(await db.Database.CanConnectAsync());
        try
        {
            var migrator = db.GetService<IMigrator>(); var now = DateTime.UtcNow;
            var host = User.Create(now); var learner = User.Create(now); var classId = Guid.NewGuid(); var archivedId = Guid.NewGuid();
            var inviteId = Guid.NewGuid(); var participantId = Guid.NewGuid(); var operation = Guid.NewGuid(); var expires = now.AddDays(7);
            if (withBaseline)
            {
                await migrator.MigrateAsync("20261101202642_ExpandMessagingAccountPolicyAndMedia");
                await LegacyAcademicMigrationFixture.InsertAsync(db, host); await LegacyAcademicMigrationFixture.InsertAsync(db, learner);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [academic].[PersonalClasses] ([Id],[HostUserId],[Title],[ClientOperationId],[CreatedAtUtc],[ArchivedAtUtc]) VALUES ({classId},{host.Id},N'قدیمی',{operation},{now},NULL),({archivedId},{host.Id},N'بایگانی',{Guid.NewGuid()},{now},{now})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [academic].[PersonalClassInvitations] ([Id],[PersonalClassId],[DestinationUserId],[ClientOperationId],[CreatedAtUtc],[ExpiresAtUtc],[AcceptedAtUtc],[RevokedAtUtc]) VALUES ({inviteId},{classId},{learner.Id},{Guid.NewGuid()},{now},{expires},{now},NULL)");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [academic].[PersonalClassParticipants] ([Id],[PersonalClassId],[StudentUserId],[InvitationId],[JoinedAtUtc],[EndedAtUtc]) VALUES ({participantId},{classId},{learner.Id},{inviteId},{now},NULL)");
            }
            await migrator.MigrateAsync(); await migrator.MigrateAsync(); db.ChangeTracker.Clear();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            if (withBaseline)
            {
                var c = await db.PersonalClasses.SingleAsync(c => c.Id == classId);
                Assert.Equal(host.Id, c.HostUserId); Assert.Equal(host.Id, c.ManagerUserId); Assert.Null(c.Capacity); Assert.False(c.AcceptingJoinRequests);
                Assert.Equal(PersonalClassMigrationState.LegacyActive, c.MigrationState);
                Assert.Equal(PersonalClassState.Archived, (await db.PersonalClasses.SingleAsync(c => c.Id == archivedId)).State);
                var participant = await db.PersonalClassParticipants.SingleAsync(m => m.Id == participantId);
                Assert.Equal(inviteId, participant.InvitationId); Assert.Equal(PersonalClassLocalRoles.Learner, participant.LocalRoles); Assert.Equal(now, participant.JoinedAtUtc);
                var invitation = await db.PersonalClassInvitations.SingleAsync(i => i.Id == inviteId); Assert.Equal(expires, invitation.ExpiresAtUtc); Assert.Equal(now, invitation.AcceptedAtUtc);
                Assert.Equal(1, await db.PersonalClassParticipants.CountAsync(m => m.PersonalClassId == classId && m.StudentUserId == host.Id && m.EndedAtUtc == null));
                Assert.Equal(2, await db.PersonalClassParticipants.CountAsync(m => m.PersonalClassId == classId));
            }
            Assert.Empty(await db.Conversations.Where(c => c.PersonalClassId != null).ToArrayAsync());
            Assert.Empty(await db.AcademicActivityPublications.ToArrayAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
