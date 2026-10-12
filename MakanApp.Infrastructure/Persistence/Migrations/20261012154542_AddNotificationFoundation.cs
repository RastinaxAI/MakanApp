using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notification");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.CreateTable(
                name: "Notifications",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResourceType = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.CheckConstraint("CK_Notifications_Category", "[Category] IN (1, 2, 3)");
                    table.CheckConstraint("CK_Notifications_ReadAt", "[ReadAtUtc] IS NULL OR [ReadAtUtc] >= [CreatedAtUtc]");
                    table.CheckConstraint("CK_Notifications_ResourceType", "[ResourceType] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Notifications_Type", "[Type] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_Notifications_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_Outbox_AttemptCount", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_Outbox_Lifecycle", "[ProcessedAtUtc] IS NULL OR ([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL AND [NextAttemptAtUtc] IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrganizationId",
                schema: "notification",
                table: "Notifications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient_Created",
                schema: "notification",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Recipient_ReadAt",
                schema: "notification",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Notifications_SourceEvent_Recipient",
                schema: "notification",
                table: "Notifications",
                columns: new[] { "SourceEventId", "RecipientUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Pending",
                schema: "platform",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAtUtc", "NextAttemptAtUtc", "ClaimedUntilUtc", "OccurredAtUtc" },
                filter: "[ProcessedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "platform");
        }
    }
}
