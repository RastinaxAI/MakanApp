using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingPresencePrivacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastSeenAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "OnlineAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddCheckConstraint(
                name: "CK_MessagingPrivacySettings_LastSeenAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings",
                sql: "[LastSeenAudience] IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MessagingPrivacySettings_OnlineAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings",
                sql: "[OnlineAudience] IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_MessagingPrivacySettings_LastSeenAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MessagingPrivacySettings_OnlineAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings");

            migrationBuilder.DropColumn(
                name: "LastSeenAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings");

            migrationBuilder.DropColumn(
                name: "OnlineAudience",
                schema: "messaging",
                table: "MessagingPrivacySettings");
        }
    }
}
