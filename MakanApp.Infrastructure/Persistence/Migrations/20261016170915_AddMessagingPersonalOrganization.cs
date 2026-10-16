using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingPersonalOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatFolders",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IncludedTypes = table.Column<int>(type: "int", nullable: false),
                    ExcludedTypes = table.Column<int>(type: "int", nullable: false),
                    UnreadOnly = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatFolders", x => x.Id);
                    table.CheckConstraint("CK_ChatFolders_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_ChatFolders_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_ChatFolders_SortOrder", "[SortOrder] BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_ChatFolders_Types", "([IncludedTypes] & ~15) = 0 AND ([ExcludedTypes] & ~15) = 0");
                    table.ForeignKey(
                        name: "FK_ChatFolders_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedMessageOrganizations",
                schema: "messaging",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedMessageOrganizations", x => x.MessageId);
                    table.CheckConstraint("CK_SavedMessageOrganizations_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_SavedMessageOrganizations_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedMessageOrganizations_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FolderConversationOverrides",
                schema: "messaging",
                columns: table => new
                {
                    FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Included = table.Column<bool>(type: "bit", nullable: false),
                    PinOrder = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderConversationOverrides", x => new { x.FolderId, x.ConversationId });
                    table.CheckConstraint("CK_FolderConversationOverrides_Pin", "[PinOrder] IS NULL OR ([Included] = 1 AND [PinOrder] BETWEEN 0 AND 199)");
                    table.ForeignKey(
                        name: "FK_FolderConversationOverrides_ChatFolders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "messaging",
                        principalTable: "ChatFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FolderConversationOverrides_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedMessageLabels",
                schema: "messaging",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, collation: "Latin1_General_100_BIN2")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedMessageLabels", x => new { x.MessageId, x.Name });
                    table.CheckConstraint("CK_SavedMessageLabels_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.ForeignKey(
                        name: "FK_SavedMessageLabels_SavedMessageOrganizations_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "messaging",
                        principalTable: "SavedMessageOrganizations",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatFolders_OwnerUserId_SortOrder_Id",
                schema: "messaging",
                table: "ChatFolders",
                columns: new[] { "OwnerUserId", "SortOrder", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_FolderConversationOverrides_ConversationId",
                schema: "messaging",
                table: "FolderConversationOverrides",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_FolderConversationOverrides_FolderId_PinOrder",
                schema: "messaging",
                table: "FolderConversationOverrides",
                columns: new[] { "FolderId", "PinOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedMessageLabels_Name_MessageId",
                schema: "messaging",
                table: "SavedMessageLabels",
                columns: new[] { "Name", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedMessageOrganizations_OwnerUserId_MessageId",
                schema: "messaging",
                table: "SavedMessageOrganizations",
                columns: new[] { "OwnerUserId", "MessageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FolderConversationOverrides",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "SavedMessageLabels",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ChatFolders",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "SavedMessageOrganizations",
                schema: "messaging");
        }
    }
}
