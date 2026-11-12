using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendProductNotificationTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notification",
                table: "NotificationPreferences");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications",
                sql: "[ResourceType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notification",
                table: "NotificationPreferences",
                sql: "[Category] BETWEEN 1 AND 8");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notification",
                table: "NotificationPreferences");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications",
                sql: "[ResourceType] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationPreferences_Category",
                schema: "notification",
                table: "NotificationPreferences",
                sql: "[Category] BETWEEN 1 AND 7");
        }
    }
}
