using MakanApp.Domain.Identity;
using MakanApp.Infrastructure.Persistence;
using MakanApp.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace MakanApp.UnitTests.Platform;

public sealed class PlatformSchemaRemovalTests
{
    [Fact]
    public void CurrentModelKeepsSessionsAndRolesButDoesNotMapSecondaryFactorState()
    {
        // ساخت metadata اتصال SQL برقرار نمی‌کند؛ این تست ادعای اجرای Migration ندارد.
        using var db = new MakanDbContext(new DbContextOptionsBuilder<MakanDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MetadataOnly;Integrated Security=True")
            .Options);
        var session = db.Model.FindEntityType(typeof(UserSession))!;
        Assert.Equal("UserSessions", session.GetTableName());
        Assert.NotNull(session.FindProperty(nameof(UserSession.TokenHash)));
        Assert.NotNull(session.FindProperty(nameof(UserSession.AbsoluteExpiresAtUtc)));
        Assert.NotNull(session.FindProperty(nameof(UserSession.RowVersion)));
        Assert.Null(session.FindProperty("AssuranceLevel"));
        Assert.Null(session.FindProperty("StepUpAtUtc"));
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity =>
            entity.GetTableName() is "PlatformMfaCredentials" or "PlatformMfaRecoveryCodes");
        Assert.Contains(db.Model.GetEntityTypes(), entity => entity.GetTableName() == "PlatformRoleAssignments");
    }

    [Fact]
    public void RemovalMigrationDropsOnlyTheApprovedTablesColumnsAndConstraint()
    {
        var operations = new RemovePlatformMfa().UpOperations;
        Assert.Equal(5, operations.Count);
        var tables = operations.OfType<DropTableOperation>().ToArray();
        Assert.Equal(2, tables.Length);
        Assert.All(tables, table => Assert.Equal("platform", table.Schema));
        Assert.Equal(new[] { "PlatformMfaCredentials", "PlatformMfaRecoveryCodes" }, tables.Select(table => table.Name).Order());
        var columns = operations.OfType<DropColumnOperation>().ToArray();
        Assert.Equal(new[] { "AssuranceLevel", "StepUpAtUtc" }, columns.Select(column => column.Name).Order());
        Assert.All(columns, column =>
        {
            Assert.Equal("identity", column.Schema);
            Assert.Equal("UserSessions", column.Table);
        });
        Assert.Equal("CK_UserSessions_AssuranceLevel", Assert.Single(operations.OfType<DropCheckConstraintOperation>()).Name);
    }

    [Fact]
    public void StructuralRollbackRestoresAValidPrimarySessionValue()
    {
        var column = Assert.Single(new RemovePlatformMfa().DownOperations.OfType<AddColumnOperation>(),
            operation => operation.Name == "AssuranceLevel");
        Assert.Equal(1, column.DefaultValue);
    }
}
