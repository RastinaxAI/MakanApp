using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformTotpMfa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformMfaCredentials",
                schema: "platform",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtectedSecret = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PendingProtectedSecret = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    EnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnrollmentSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnrollmentExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAcceptedTimeStep = table.Column<long>(type: "bigint", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    LockedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformMfaCredentials", x => x.UserId);
                    table.CheckConstraint("CK_PlatformMfa_Active", "([ProtectedSecret] IS NULL AND [ActivatedAtUtc] IS NULL) OR ([ProtectedSecret] IS NOT NULL AND [ActivatedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_PlatformMfa_Attempts", "[FailedAttempts] >= 0 AND ([LastAcceptedTimeStep] IS NULL OR [LastAcceptedTimeStep] >= 0)");
                    table.CheckConstraint("CK_PlatformMfa_Pending", "([PendingProtectedSecret] IS NULL AND [EnrollmentId] IS NULL AND [EnrollmentSessionId] IS NULL AND [EnrollmentExpiresAtUtc] IS NULL) OR ([PendingProtectedSecret] IS NOT NULL AND [EnrollmentId] IS NOT NULL AND [EnrollmentSessionId] IS NOT NULL AND [EnrollmentExpiresAtUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PlatformMfaCredentials_UserSessions_EnrollmentSessionId_UserId",
                        columns: x => new { x.EnrollmentSessionId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformMfaCredentials_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformMfaRecoveryCodes",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformMfaRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlatformMfaRecoveryCodes_PlatformMfaCredentials_UserId",
                        column: x => x.UserId,
                        principalSchema: "platform",
                        principalTable: "PlatformMfaCredentials",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformMfaCredentials_EnrollmentSessionId_UserId",
                schema: "platform",
                table: "PlatformMfaCredentials",
                columns: new[] { "EnrollmentSessionId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformMfaRecoveryCodes_UserId_CodeHash",
                schema: "platform",
                table: "PlatformMfaRecoveryCodes",
                columns: new[] { "UserId", "CodeHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformMfaRecoveryCodes",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformMfaCredentials",
                schema: "platform");
        }
    }
}
