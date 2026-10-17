using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationChecklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SafetyIncidents_ContentKind",
                schema: "messaging",
                table: "SafetyIncidents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AbuseReports_MessageKind",
                schema: "messaging",
                table: "AbuseReports");

            migrationBuilder.CreateTable(
                name: "ConversationChecklists",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    CreationHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    DefinitionRevision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationChecklists", x => x.Id);
                    table.UniqueConstraint("AK_ConversationChecklists_Id_Mode", x => new { x.Id, x.Mode });
                    table.CheckConstraint("CK_ConversationChecklists_Definition", "[Mode] IN (1,2) AND [DefinitionRevision]>0 AND DATALENGTH([CreationHash])=32 AND [UpdatedAtUtc]>=[CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ConversationChecklists_Messages_Id_ConversationId",
                        columns: x => new { x.Id, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationChecklists_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationChecklistItems",
                schema: "messaging",
                columns: table => new
                {
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    CreatedAtRevision = table.Column<long>(type: "bigint", nullable: false),
                    RetiredAtRevision = table.Column<long>(type: "bigint", nullable: true),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    StateRevision = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationChecklistItems", x => new { x.ChecklistId, x.Id });
                    table.UniqueConstraint("AK_ConversationChecklistItems_ChecklistId_Id_Mode", x => new { x.ChecklistId, x.Id, x.Mode });
                    table.CheckConstraint("CK_ConversationChecklistItems_Definition", "LEN(LTRIM(RTRIM([Text])))>0 AND [Position] BETWEEN 1 AND 20 AND [CreatedAtRevision]>0 AND ([RetiredAtRevision] IS NULL OR [RetiredAtRevision]>[CreatedAtRevision])");
                    table.CheckConstraint("CK_ConversationChecklistItems_State", "[StateRevision]>0 AND ([Mode]=2 OR ([Mode]=1 AND [IsChecked]=0 AND [StateRevision]=1))");
                    table.ForeignKey(
                        name: "FK_ConversationChecklistItems_ConversationChecklists_ChecklistId_Mode",
                        columns: x => new { x.ChecklistId, x.Mode },
                        principalSchema: "messaging",
                        principalTable: "ConversationChecklists",
                        principalColumns: new[] { "Id", "Mode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationChecklistPersonalStates",
                schema: "messaging",
                columns: table => new
                {
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationChecklistPersonalStates", x => new { x.ChecklistId, x.ItemId, x.UserId });
                    table.CheckConstraint("CK_ConversationChecklistPersonalStates_Mode", "[Mode]=1 AND [Revision]>0");
                    table.ForeignKey(
                        name: "FK_ConversationChecklistPersonalStates_ConversationChecklistItems_ChecklistId_ItemId_Mode",
                        columns: x => new { x.ChecklistId, x.ItemId, x.Mode },
                        principalSchema: "messaging",
                        principalTable: "ConversationChecklistItems",
                        principalColumns: new[] { "ChecklistId", "Id", "Mode" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationChecklistPersonalStates_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SafetyIncidents_ContentKind",
                schema: "messaging",
                table: "SafetyIncidents",
                sql: "[ContentKind] IN (1, 2, 3, 4, 5, 6, 7, 8)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages",
                sql: "([DeletedAtUtc] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [Text] IS NULL) OR ([DeletedAtUtc] IS NULL AND [DeletedByUserId] IS NULL AND (([Kind] IN (1, 7, 8) AND [Text] IS NOT NULL AND LEN(LTRIM(RTRIM([Text]))) > 0) OR ([Kind] IN (2, 3, 4, 5) AND ([Text] IS NULL OR LEN(LTRIM(RTRIM([Text]))) > 0)) OR ([Kind] = 6 AND [Text] IS NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages",
                sql: "[Kind] IN (1, 2, 3, 4, 5, 6, 7, 8)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AbuseReports_MessageKind",
                schema: "messaging",
                table: "AbuseReports",
                sql: "[ReportedMessageKind] IN (1, 2, 3, 4, 5, 6, 7, 8)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklistItems_ChecklistId_Mode",
                schema: "messaging",
                table: "ConversationChecklistItems",
                columns: new[] { "ChecklistId", "Mode" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklistItems_ChecklistId_RetiredAtRevision_Position",
                schema: "messaging",
                table: "ConversationChecklistItems",
                columns: new[] { "ChecklistId", "RetiredAtRevision", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklistPersonalStates_ChecklistId_ItemId_Mode",
                schema: "messaging",
                table: "ConversationChecklistPersonalStates",
                columns: new[] { "ChecklistId", "ItemId", "Mode" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklistPersonalStates_ChecklistId_UserId_ItemId",
                schema: "messaging",
                table: "ConversationChecklistPersonalStates",
                columns: new[] { "ChecklistId", "UserId", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklistPersonalStates_UserId",
                schema: "messaging",
                table: "ConversationChecklistPersonalStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklists_ConversationId_CreatedAtUtc_Id",
                schema: "messaging",
                table: "ConversationChecklists",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklists_CreatorUserId",
                schema: "messaging",
                table: "ConversationChecklists",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationChecklists_Id_ConversationId",
                schema: "messaging",
                table: "ConversationChecklists",
                columns: new[] { "Id", "ConversationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // rollback دادهٔ تعاملی/شاهد را حذف نمی‌کند؛ پس از مصرف قابلیت باید forward-fix یا restore مصوب انجام شود.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [messaging].[ConversationChecklists])
                   OR EXISTS (SELECT 1 FROM [messaging].[ConversationChecklistItems])
                   OR EXISTS (SELECT 1 FROM [messaging].[ConversationChecklistPersonalStates])
                   OR EXISTS (SELECT 1 FROM [messaging].[Messages] WHERE [Kind]=8)
                   OR EXISTS (SELECT 1 FROM [messaging].[ChangeEvents] WHERE [ChangeType]=15)
                   OR EXISTS (SELECT 1 FROM [messaging].[AbuseReports] WHERE [ReportedMessageKind]=8)
                   OR EXISTS (SELECT 1 FROM [messaging].[SafetyIncidents] WHERE [ContentKind]=8)
                    THROW 51071, 'Checklist data or evidence exists; destructive rollback refused.', 1;
                """);
            migrationBuilder.DropTable(
                name: "ConversationChecklistPersonalStates",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationChecklistItems",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationChecklists",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SafetyIncidents_ContentKind",
                schema: "messaging",
                table: "SafetyIncidents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AbuseReports_MessageKind",
                schema: "messaging",
                table: "AbuseReports");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SafetyIncidents_ContentKind",
                schema: "messaging",
                table: "SafetyIncidents",
                sql: "[ContentKind] IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages",
                sql: "([DeletedAtUtc] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [Text] IS NULL) OR ([DeletedAtUtc] IS NULL AND [DeletedByUserId] IS NULL AND (([Kind] IN (1, 7) AND LEN(LTRIM(RTRIM([Text]))) > 0) OR ([Kind] IN (2, 3, 4, 5) AND ([Text] IS NULL OR LEN(LTRIM(RTRIM([Text]))) > 0)) OR ([Kind] = 6 AND [Text] IS NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages",
                sql: "[Kind] IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AbuseReports_MessageKind",
                schema: "messaging",
                table: "AbuseReports",
                sql: "[ReportedMessageKind] IN (1, 2, 3, 4, 5, 6, 7)");
        }
    }
}
