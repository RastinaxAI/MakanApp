using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveAiConversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiConversations",
                schema: "intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContextFingerprint = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    SourcePins = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    NextSequence = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversations", x => x.Id);
                    table.UniqueConstraint("AK_AiConversations_Id_OwnerUserId", x => new { x.Id, x.OwnerUserId });
                    table.CheckConstraint("CK_AiConversations_Sequence", "[NextSequence] >= 1 AND [NextSequence] % 2 = 1");
                    table.CheckConstraint("CK_AiConversations_Sources", "ISJSON([SourcePins]) = 1");
                    table.ForeignKey(
                        name: "FK_AiConversations_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiConversationTurns",
                schema: "intelligence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    IntentHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiConversationTurns", x => x.Id);
                    table.CheckConstraint("CK_AiConversationTurns_Completion", "([IsCompleted]=0 AND [Role]=1 AND [Text] IS NULL AND [CompletedAtUtc] IS NULL) OR ([IsCompleted]=1 AND [Text] IS NOT NULL AND DATALENGTH([Text])>0 AND [CompletedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_AiConversationTurns_RoleSequence", "([Role]=1 AND [Sequence]>0 AND [Sequence]%2=1) OR ([Role]=2 AND [Sequence]>0 AND [Sequence]%2=0)");
                    table.CheckConstraint("CK_AiConversationTurns_TextBound", "[Text] IS NULL OR ([Role]=1 AND DATALENGTH([Text])<=16000) OR ([Role]=2 AND DATALENGTH([Text])<=131072)");
                    table.ForeignKey(
                        name: "FK_AiConversationTurns_AiConversations_ConversationId_OwnerUserId",
                        columns: x => new { x.ConversationId, x.OwnerUserId },
                        principalSchema: "intelligence",
                        principalTable: "AiConversations",
                        principalColumns: new[] { "Id", "OwnerUserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversations_OwnerUserId_UpdatedAtUtc_Id",
                schema: "intelligence",
                table: "AiConversations",
                columns: new[] { "OwnerUserId", "UpdatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationTurns_ConversationId_OwnerUserId",
                schema: "intelligence",
                table: "AiConversationTurns",
                columns: new[] { "ConversationId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationTurns_ConversationId_Sequence",
                schema: "intelligence",
                table: "AiConversationTurns",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiConversationTurns_OwnerUserId_OperationId_Role",
                schema: "intelligence",
                table: "AiConversationTurns",
                columns: new[] { "OwnerUserId", "OperationId", "Role" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [intelligence].[AiConversations])
                    OR EXISTS (SELECT 1 FROM [intelligence].[AiConversationTurns])
                    THROW 51000, 'Adaptive AI history must be preserved before schema rollback.', 1;
                """);
            migrationBuilder.DropTable(
                name: "AiConversationTurns",
                schema: "intelligence");

            migrationBuilder.DropTable(
                name: "AiConversations",
                schema: "intelligence");
        }
    }
}
