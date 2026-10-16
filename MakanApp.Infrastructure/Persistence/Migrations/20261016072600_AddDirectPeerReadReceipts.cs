using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDirectPeerReadReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)");
        }
    }
}
