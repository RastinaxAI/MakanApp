using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceAssistantAndDeletionSessionOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountDeletionRequests_UserSessions_RequestedBySessionId",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantContextSelections_UserSessions_SessionId",
                schema: "intelligence",
                table: "AssistantContextSelections");

            migrationBuilder.DropIndex(
                name: "IX_AccountDeletionRequests_RequestedBySessionId",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantContextSelections_SessionId_UserId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                columns: new[] { "SessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionRequests_RequestedBySessionId_UserId",
                schema: "identity",
                table: "AccountDeletionRequests",
                columns: new[] { "RequestedBySessionId", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountDeletionRequests_UserSessions_RequestedBySessionId_UserId",
                schema: "identity",
                table: "AccountDeletionRequests",
                columns: new[] { "RequestedBySessionId", "UserId" },
                principalSchema: "identity",
                principalTable: "UserSessions",
                principalColumns: new[] { "Id", "UserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantContextSelections_UserSessions_SessionId_UserId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                columns: new[] { "SessionId", "UserId" },
                principalSchema: "identity",
                principalTable: "UserSessions",
                principalColumns: new[] { "Id", "UserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountDeletionRequests_UserSessions_RequestedBySessionId_UserId",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantContextSelections_UserSessions_SessionId_UserId",
                schema: "intelligence",
                table: "AssistantContextSelections");

            migrationBuilder.DropIndex(
                name: "IX_AssistantContextSelections_SessionId_UserId",
                schema: "intelligence",
                table: "AssistantContextSelections");

            migrationBuilder.DropIndex(
                name: "IX_AccountDeletionRequests_RequestedBySessionId_UserId",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionRequests_RequestedBySessionId",
                schema: "identity",
                table: "AccountDeletionRequests",
                column: "RequestedBySessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountDeletionRequests_UserSessions_RequestedBySessionId",
                schema: "identity",
                table: "AccountDeletionRequests",
                column: "RequestedBySessionId",
                principalSchema: "identity",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantContextSelections_UserSessions_SessionId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                column: "SessionId",
                principalSchema: "identity",
                principalTable: "UserSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
