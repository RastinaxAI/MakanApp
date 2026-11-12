using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedPhoneChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhoneChangeRequests",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialVersion = table.Column<byte[]>(type: "varbinary(8)", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CodeHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    Salt = table.Column<byte[]>(type: "varbinary(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false),
                    MaxFailedAttempts = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneChangeRequests", x => x.Id);
                    table.CheckConstraint("CK_PhoneChangeRequests_Attempts", "[FailedAttempts] >= 0 AND [MaxFailedAttempts] > 0 AND [FailedAttempts] <= [MaxFailedAttempts]");
                    table.CheckConstraint("CK_PhoneChangeRequests_Completed", "([State] = 3 AND [CompletedAtUtc] IS NOT NULL) OR ([State] <> 3 AND [CompletedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_PhoneChangeRequests_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_PhoneChangeRequests_Hash", "[State] IN (1,4) OR (DATALENGTH([CodeHash]) = 32 AND DATALENGTH([Salt]) >= 16)");
                    table.CheckConstraint("CK_PhoneChangeRequests_State", "[State] IN (1,2,3,4)");
                    table.ForeignKey(
                        name: "FK_PhoneChangeRequests_UserCredentials_CredentialId",
                        column: x => x.CredentialId,
                        principalSchema: "identity",
                        principalTable: "UserCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhoneChangeRequests_UserSessions_SessionId_UserId",
                        columns: x => new { x.SessionId, x.UserId },
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhoneChangeRequests_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneChangeRequests_CredentialId",
                schema: "identity",
                table: "PhoneChangeRequests",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneChangeRequests_SessionId_UserId",
                schema: "identity",
                table: "PhoneChangeRequests",
                columns: new[] { "SessionId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneChangeRequests_TargetPhone_CreatedAtUtc",
                schema: "identity",
                table: "PhoneChangeRequests",
                columns: new[] { "TargetPhone", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneChangeRequests_UserId_ClientOperationId",
                schema: "identity",
                table: "PhoneChangeRequests",
                columns: new[] { "UserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhoneChangeRequests_UserId_CreatedAtUtc",
                schema: "identity",
                table: "PhoneChangeRequests",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PhoneChangeRequests_PendingUser",
                schema: "identity",
                table: "PhoneChangeRequests",
                column: "UserId",
                unique: true,
                filter: "[State] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoneChangeRequests",
                schema: "identity");
        }
    }
}
