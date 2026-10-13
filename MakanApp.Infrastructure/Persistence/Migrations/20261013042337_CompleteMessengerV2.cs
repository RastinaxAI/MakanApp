using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteMessengerV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_BehaviorShape",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_ManagedShape",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.AddColumn<string>(
                name: "SharedContactDisplayName",
                schema: "messaging",
                table: "Messages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SharedContactUserId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedContactUsername",
                schema: "messaging",
                table: "Messages",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "HistoryHiddenThroughSequence",
                schema: "messaging",
                table: "ConversationUserStates",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [messaging].[ConversationUserStates]
                SET [HistoryHiddenThroughSequence] = 0
                WHERE [HistoryHiddenThroughSequence] IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "HistoryHiddenThroughSequence",
                schema: "messaging",
                table: "ConversationUserStates",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SavedOwnerUserId",
                schema: "messaging",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications",
                sql: "[ResourceType] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_SharedContactUserId",
                schema: "messaging",
                table: "Messages",
                column: "SharedContactUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_ContactCardShape",
                schema: "messaging",
                table: "Messages",
                sql: "([Kind] = 6 AND [SharedContactUserId] IS NOT NULL AND LEN(LTRIM(RTRIM([SharedContactUsername]))) > 0 AND LEN(LTRIM(RTRIM([SharedContactDisplayName]))) > 0) OR ([Kind] <> 6 AND [SharedContactUserId] IS NULL AND [SharedContactUsername] IS NULL AND [SharedContactDisplayName] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages",
                sql: "([DeletedAtUtc] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [Text] IS NULL) OR ([DeletedAtUtc] IS NULL AND [DeletedByUserId] IS NULL AND (([Kind] = 1 AND LEN(LTRIM(RTRIM([Text]))) > 0) OR ([Kind] IN (2, 3, 4, 5) AND ([Text] IS NULL OR LEN(LTRIM(RTRIM([Text]))) > 0)) OR ([Kind] = 6 AND [Text] IS NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages",
                sql: "[Kind] IN (1, 2, 3, 4, 5, 6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationUserStates_HistoryHiddenThroughSequence",
                schema: "messaging",
                table: "ConversationUserStates",
                sql: "[HistoryHiddenThroughSequence] >= 0");

            migrationBuilder.CreateIndex(
                name: "UX_Conversations_Saved_Owner",
                schema: "messaging",
                table: "Conversations",
                column: "SavedOwnerUserId",
                unique: true,
                filter: "[Type] = 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_BehaviorShape",
                schema: "messaging",
                table: "Conversations",
                sql: "([Type] IN (1, 4) AND [Visibility] = 1 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0) OR ([Type] = 2) OR ([Type] = 3 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_ManagedShape",
                schema: "messaging",
                table: "Conversations",
                sql: "([Type] IN (1, 4) AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] IN (2, 3) AND [ManagementPolicy] IN (1, 2) AND LEN(LTRIM(RTRIM([Title]))) > 0 AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_SavedShape",
                schema: "messaging",
                table: "Conversations",
                sql: "([Type] = 4 AND [Scope] = 1 AND [OrganizationId] IS NULL AND [SavedOwnerUserId] IS NOT NULL AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] <> 4 AND [SavedOwnerUserId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations",
                sql: "[Type] IN (1, 2, 3, 4)");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Users_SavedOwnerUserId",
                schema: "messaging",
                table: "Conversations",
                column: "SavedOwnerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Users_SharedContactUserId",
                schema: "messaging",
                table: "Messages",
                column: "SharedContactUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Users_SavedOwnerUserId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Users_SharedContactUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Messages_SharedContactUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_ContactCardShape",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationUserStates_HistoryHiddenThroughSequence",
                schema: "messaging",
                table: "ConversationUserStates");

            migrationBuilder.DropIndex(
                name: "UX_Conversations_Saved_Owner",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_BehaviorShape",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_ManagedShape",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_SavedShape",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SharedContactDisplayName",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SharedContactUserId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SharedContactUsername",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "HistoryHiddenThroughSequence",
                schema: "messaging",
                table: "ConversationUserStates");

            migrationBuilder.DropColumn(
                name: "SavedOwnerUserId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_ResourceType",
                schema: "notification",
                table: "Notifications",
                sql: "[ResourceType] IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Notifications_Type",
                schema: "notification",
                table: "Notifications",
                sql: "[Type] IN (1, 2, 3, 4)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Content",
                schema: "messaging",
                table: "Messages",
                sql: "([DeletedAtUtc] IS NOT NULL AND [DeletedByUserId] IS NOT NULL AND [Text] IS NULL) OR ([DeletedAtUtc] IS NULL AND [DeletedByUserId] IS NULL AND (([Kind] = 1 AND LEN(LTRIM(RTRIM([Text]))) > 0) OR ([Kind] IN (2, 3, 4, 5) AND ([Text] IS NULL OR LEN(LTRIM(RTRIM([Text]))) > 0))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Messages_Kind",
                schema: "messaging",
                table: "Messages",
                sql: "[Kind] IN (1, 2, 3, 4, 5)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_BehaviorShape",
                schema: "messaging",
                table: "Conversations",
                sql: "([Type] = 1 AND [Visibility] = 1 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0) OR ([Type] = 2) OR ([Type] = 3 AND [GroupHistoryPolicy] = 1 AND [MemberCapabilities] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_ManagedShape",
                schema: "messaging",
                table: "Conversations",
                sql: "([Type] = 1 AND [ManagementPolicy] = 0 AND [Title] IS NULL) OR ([Type] IN (2, 3) AND [ManagementPolicy] IN (1, 2) AND LEN(LTRIM(RTRIM([Title]))) > 0 AND [DirectUserLowId] IS NULL AND [DirectUserHighId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_Type",
                schema: "messaging",
                table: "Conversations",
                sql: "[Type] IN (1, 2, 3)");
        }
    }
}
