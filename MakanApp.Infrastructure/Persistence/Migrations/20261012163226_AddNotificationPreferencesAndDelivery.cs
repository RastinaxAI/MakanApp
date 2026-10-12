using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferencesAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_UserSessions_Id_UserId",
                schema: "identity",
                table: "UserSessions",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryAttempts",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    DestinationReference = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FailureReason = table.Column<int>(type: "int", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryAttempts", x => x.Id);
                    table.CheckConstraint("CK_NotificationDeliveryAttempts_AttemptCount", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_NotificationDeliveryAttempts_Channel", "[Channel] IN (1, 2)");
                    table.CheckConstraint("CK_NotificationDeliveryAttempts_Delivered", "([Status] = 4 AND [DeliveredAtUtc] IS NOT NULL) OR ([Status] <> 4 AND [DeliveredAtUtc] IS NULL)");
                    table.CheckConstraint("CK_NotificationDeliveryAttempts_FailureReason", "[FailureReason] IS NULL OR [FailureReason] BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_NotificationDeliveryAttempts_Lifecycle", "([Status] = 2 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL) OR ([Status] <> 2 AND [ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL)");
                    table.CheckConstraint("CK_NotificationDeliveryAttempts_Status", "[Status] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryAttempts_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalSchema: "notification",
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    PushEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    QuietHoursEnabled = table.Column<bool>(type: "bit", nullable: false),
                    QuietHoursStartLocal = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    QuietHoursEndLocal = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.CheckConstraint("CK_NotificationPreferences_Category", "[Category] BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_NotificationPreferences_QuietHours", "([QuietHoursEnabled] = 0 AND [QuietHoursStartLocal] IS NULL AND [QuietHoursEndLocal] IS NULL AND [TimeZoneId] IS NULL) OR ([QuietHoursEnabled] = 1 AND [QuietHoursStartLocal] IS NOT NULL AND [QuietHoursEndLocal] IS NOT NULL AND [QuietHoursStartLocal] <> [QuietHoursEndLocal] AND [TimeZoneId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushDeviceRegistrations",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    PushToken = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDeviceRegistrations", x => x.Id);
                    table.CheckConstraint("CK_PushDeviceRegistrations_Platform", "[Platform] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_PushDeviceRegistrations_UserSessions_UserSessionId_UserId",
                        columns: x => new { x.UserSessionId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PushDeviceRegistrations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_Pending",
                schema: "notification",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "Status", "NextAttemptAtUtc", "ClaimedUntilUtc", "CreatedAtUtc" },
                filter: "[Status] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationDeliveryAttempts_StableDestination",
                schema: "notification",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "NotificationId", "Channel", "DestinationReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_OrganizationId",
                schema: "notification",
                table: "NotificationPreferences",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationPreferences_User_Organization_Category",
                schema: "notification",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "OrganizationId", "Category" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_NotificationPreferences_User_Personal_Category",
                schema: "notification",
                table: "NotificationPreferences",
                columns: new[] { "UserId", "Category" },
                unique: true,
                filter: "[OrganizationId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceRegistrations_User_Active",
                schema: "notification",
                table: "PushDeviceRegistrations",
                columns: new[] { "UserId", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PushDeviceRegistrations_UserSessionId_UserId",
                schema: "notification",
                table: "PushDeviceRegistrations",
                columns: new[] { "UserSessionId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "UX_PushDeviceRegistrations_ActiveToken",
                schema: "notification",
                table: "PushDeviceRegistrations",
                column: "TokenHash",
                unique: true,
                filter: "[RevokedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationDeliveryAttempts",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "NotificationPreferences",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "PushDeviceRegistrations",
                schema: "notification");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_UserSessions_Id_UserId",
                schema: "identity",
                table: "UserSessions");
        }
    }
}
