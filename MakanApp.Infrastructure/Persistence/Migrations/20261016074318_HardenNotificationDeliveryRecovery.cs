using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenNotificationDeliveryRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationDeliveryAttempts_FailureReason",
                schema: "notification",
                table: "NotificationDeliveryAttempts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationDeliveryAttempts_FailureReason",
                schema: "notification",
                table: "NotificationDeliveryAttempts",
                sql: "[FailureReason] IS NULL OR [FailureReason] BETWEEN 1 AND 13");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationDeliveryAttempts_FailureReason",
                schema: "notification",
                table: "NotificationDeliveryAttempts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationDeliveryAttempts_FailureReason",
                schema: "notification",
                table: "NotificationDeliveryAttempts",
                sql: "[FailureReason] IS NULL OR [FailureReason] BETWEEN 1 AND 8");
        }
    }
}
