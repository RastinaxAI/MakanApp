using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindPersonalClassInvitationRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DelegatedCapabilities",
                schema: "academic",
                table: "PersonalClassInvitations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LocalRoles",
                schema: "academic",
                table: "PersonalClassInvitations",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClassInvitations_Delegation",
                schema: "academic",
                table: "PersonalClassInvitations",
                sql: "[DelegatedCapabilities] BETWEEN 0 AND 3 AND ([DelegatedCapabilities]=0 OR ([LocalRoles]&2)=2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClassInvitations_Roles",
                schema: "academic",
                table: "PersonalClassInvitations",
                sql: "[LocalRoles] BETWEEN 1 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [academic].[PersonalClassInvitations] WHERE [LocalRoles]<>1 OR [DelegatedCapabilities]<>0) THROW 51161, 'Personal class invitation roles require a reviewed data-preserving rollback.', 1;");
            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClassInvitations_Delegation",
                schema: "academic",
                table: "PersonalClassInvitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClassInvitations_Roles",
                schema: "academic",
                table: "PersonalClassInvitations");

            migrationBuilder.DropColumn(
                name: "DelegatedCapabilities",
                schema: "academic",
                table: "PersonalClassInvitations");

            migrationBuilder.DropColumn(
                name: "LocalRoles",
                schema: "academic",
                table: "PersonalClassInvitations");
        }
    }
}
