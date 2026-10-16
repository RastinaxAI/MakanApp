using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.AddColumn<Guid>(
                name: "TopicId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationTopics",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationTopics", x => x.Id);
                    table.UniqueConstraint("AK_ConversationTopics_Id_ConversationId", x => new { x.Id, x.ConversationId });
                    table.CheckConstraint("CK_ConversationTopics_Revision", "[Revision] > 0");
                    table.CheckConstraint("CK_ConversationTopics_Status", "[Status] IN (1,2)");
                    table.CheckConstraint("CK_ConversationTopics_Title", "LEN(LTRIM(RTRIM([Title]))) > 0");
                    table.ForeignKey(
                        name: "FK_ConversationTopics_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationTopics_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId_TopicId_Sequence",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "ConversationId", "TopicId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TopicId_ConversationId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "TopicId", "ConversationId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[AdminPermissions] BETWEEN 0 AND 8191 AND (([Role] = 2) OR [AdminPermissions] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTopics_ConversationId_CreatedAtUtc_Id",
                schema: "messaging",
                table: "ConversationTopics",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationTopics_CreatedByUserId",
                schema: "messaging",
                table: "ConversationTopics",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ConversationTopics_TopicId_ConversationId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "TopicId", "ConversationId" },
                principalSchema: "messaging",
                principalTable: "ConversationTopics",
                principalColumns: new[] { "Id", "ConversationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // rollback نباید پس از تغییر داده، به CHECK قدیمی برخورد کند یا ساختار تاریخی را بی‌اجازه حذف کند.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [messaging].[ConversationParticipants] WHERE ([AdminPermissions] & 4096) <> 0) OR EXISTS (SELECT 1 FROM [messaging].[ChangeEvents] WHERE [ChangeType] = 13) OR EXISTS (SELECT 1 FROM [messaging].[ConversationTopics]) THROW 51000, N'موضوع‌ها و مجوزهای جدید پیش از rollback نیازمند تصمیم عملیاتی و backup هستند.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_ConversationTopics_TopicId_ConversationId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "ConversationTopics",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ConversationId_TopicId_Sequence",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TopicId_ConversationId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents");

            migrationBuilder.DropColumn(
                name: "TopicId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[AdminPermissions] BETWEEN 0 AND 4095 AND (([Role] = 2) OR [AdminPermissions] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)");
        }
    }
}
