using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenMessengerConversationBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.AddColumn<int?>(
                name: "GroupHistoryPolicy",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int?>(
                name: "MemberCapabilities",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int?>(
                name: "Visibility",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int?>(
                name: "AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long?>(
                name: "HistoryVisibleFromSequence",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [messaging].[Conversations]
                SET [Visibility] = 1,
                    [GroupHistoryPolicy] = 1,
                    [MemberCapabilities] = CASE WHEN [Type] = 2 THEN 3 ELSE 0 END;

                UPDATE [messaging].[ConversationParticipants]
                SET [HistoryVisibleFromSequence] = 1,
                    [AdminPermissions] = CASE WHEN [Role] = 2 THEN 51 ELSE 0 END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "GroupHistoryPolicy",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MemberCapabilities",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Visibility",
                schema: "messaging",
                table: "Conversations",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "HistoryVisibleFromSequence",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationUserStates",
                schema: "messaging",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    PinnedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    MuteMode = table.Column<int>(type: "int", nullable: false),
                    MutedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    IsManuallyUnread = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationUserStates", x => new { x.ConversationId, x.UserId });
                    table.CheckConstraint("CK_ConversationUserStates_MuteMode", "[MuteMode] IN (0, 1, 2)");
                    table.CheckConstraint("CK_ConversationUserStates_MuteShape", "([MuteMode] IN (0, 1) AND [MutedUntilUtc] IS NULL) OR ([MuteMode] = 2 AND [MutedUntilUtc] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ConversationUserStates_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationUserStates_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MessageUserVisibilities",
                schema: "messaging",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HiddenAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageUserVisibilities", x => new { x.MessageId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MessageUserVisibilities_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageUserVisibilities_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_PublicDiscovery",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "Scope", "OrganizationId", "Visibility", "Type", "Status", "CreatedAtUtc", "Id" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_BehaviorShape",
                schema: "messaging",
                table: "Conversations",
                sql: "([Type] = 1 AND [Visibility] = 1 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0) OR ([Type] = 2) OR ([Type] = 3 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_GroupHistoryPolicy",
                schema: "messaging",
                table: "Conversations",
                sql: "[GroupHistoryPolicy] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_MemberCapabilities",
                schema: "messaging",
                table: "Conversations",
                sql: "[MemberCapabilities] BETWEEN 0 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_SystemManagedVisibility",
                schema: "messaging",
                table: "Conversations",
                sql: "[ManagementPolicy] <> 2 OR [Visibility] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_Visibility",
                schema: "messaging",
                table: "Conversations",
                sql: "[Visibility] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationParticipants_ConversationId_UserId_HistoryVisibleFromSequence",
                schema: "messaging",
                table: "ConversationParticipants",
                columns: new[] { "ConversationId", "UserId", "HistoryVisibleFromSequence" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[AdminPermissions] BETWEEN 0 AND 255 AND (([Role] = 2) OR [AdminPermissions] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_HistoryBoundary",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[HistoryVisibleFromSequence] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationUserStates_User_List",
                schema: "messaging",
                table: "ConversationUserStates",
                columns: new[] { "UserId", "ArchivedAtUtc", "PinnedAtUtc", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageUserVisibilities_User_Message",
                schema: "messaging",
                table: "MessageUserVisibilities",
                columns: new[] { "UserId", "MessageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationUserStates",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "MessageUserVisibilities",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_PublicDiscovery",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_BehaviorShape",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_GroupHistoryPolicy",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_MemberCapabilities",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_SystemManagedVisibility",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_Visibility",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ConversationParticipants_ConversationId_UserId_HistoryVisibleFromSequence",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_HistoryBoundary",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.DropColumn(
                name: "GroupHistoryPolicy",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "MemberCapabilities",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropColumn(
                name: "HistoryVisibleFromSequence",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9)");
        }
    }
}
