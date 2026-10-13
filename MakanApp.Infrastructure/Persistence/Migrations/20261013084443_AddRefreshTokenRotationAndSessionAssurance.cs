using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenRotationAndSessionAssurance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserId",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "AbsoluteExpiresAtUtc",
                schema: "identity",
                table: "UserSessions",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssuranceLevel",
                schema: "identity",
                table: "UserSessions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceName",
                schema: "identity",
                table: "UserSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StepUpAtUtc",
                schema: "identity",
                table: "UserSessions",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [identity].[UserSessions] SET [AbsoluteExpiresAtUtc] = [ExpiresAtUtc], [AssuranceLevel] = 1;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AbsoluteExpiresAtUtc",
                schema: "identity",
                table: "UserSessions",
                type: "datetime2(7)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2(7)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AssuranceLevel",
                schema: "identity",
                table: "UserSessions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.CheckConstraint("CK_RefreshTokens_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_RefreshTokens_Lifecycle", "[UsedAtUtc] IS NULL OR [ReplacedByTokenId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalSchema: "identity",
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_UserSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_User_Revoked_AbsoluteExpiry",
                schema: "identity",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedAtUtc", "AbsoluteExpiresAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSessions_AssuranceLevel",
                schema: "identity",
                table: "UserSessions",
                sql: "[AssuranceLevel] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReplacedByTokenId",
                schema: "identity",
                table: "RefreshTokens",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Session_ExpiresAtUtc",
                schema: "identity",
                table: "RefreshTokens",
                columns: new[] { "SessionId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_RefreshTokens_TokenHash",
                schema: "identity",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_User_Revoked_AbsoluteExpiry",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSessions_AssuranceLevel",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "AbsoluteExpiresAtUtc",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "AssuranceLevel",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "DeviceName",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "StepUpAtUtc",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                schema: "identity",
                table: "UserSessions",
                column: "UserId");
        }
    }
}
