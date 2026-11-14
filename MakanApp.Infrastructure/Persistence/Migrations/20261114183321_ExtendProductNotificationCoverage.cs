using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendProductNotificationCoverage : Migration
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

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications",
                sql: "[ResourceType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // receipt جدید حذف یا به نوع قدیمی تبدیل نمی‌شود؛ بازگشت پس از مصرف قرارداد تازه مجاز نیست.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [notification].[Notifications] WHERE [Type] > 12 OR [ResourceType] > 10)
                    OR EXISTS (SELECT 1 FROM [platform].[OutboxMessages] WHERE [MessageType] IN (
                        N'notification.conversation-invitation.v1', N'notification.conversation-join-request-updated.v1',
                        N'notification.personal-class-request-updated.v1', N'notification.personal-class-invitation.v1',
                        N'notification.personal-class-join-request-updated.v1', N'notification.personal-class-manager-transfer-updated.v1',
                        N'notification.personal-class-membership-changed.v1', N'notification.personal-class-state-changed.v1'))
                    THROW 51000, N'بازگشت قرارداد اعلان با receipt جدید مجاز نیست؛ اصلاح رو به جلو لازم است.', 1;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications");

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
        }
    }
}
