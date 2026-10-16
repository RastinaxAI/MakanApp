using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationPollsAndQuiz : Migration
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
                name: "ConversationPolls",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsQuiz = table.Column<bool>(type: "bit", nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    AllowsMultipleAnswers = table.Column<bool>(type: "bit", nullable: false),
                    OptionCount = table.Column<int>(type: "int", nullable: false),
                    DefinitionHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationPolls", x => x.Id);
                    table.UniqueConstraint("AK_ConversationPolls_Id_IsQuiz", x => new { x.Id, x.IsQuiz });
                    table.CheckConstraint("CK_ConversationPolls_Definition", "[OptionCount] BETWEEN 2 AND 10 AND DATALENGTH([DefinitionHash])=32 AND ([IsQuiz]=0 OR [AllowsMultipleAnswers]=0)");
                    table.CheckConstraint("CK_ConversationPolls_State", "[Revision]>0 AND [UpdatedAtUtc]>=[CreatedAtUtc] AND (([Status]=1 AND [ClosedAtUtc] IS NULL AND [ClosedByUserId] IS NULL) OR ([Status]=2 AND [ClosedAtUtc] IS NOT NULL AND [ClosedAtUtc]>=[CreatedAtUtc] AND [ClosedByUserId] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ConversationPolls_Messages_Id_ConversationId",
                        columns: x => new { x.Id, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationPolls_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationPolls_Users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationPollOptions",
                schema: "messaging",
                columns: table => new
                {
                    PollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationPollOptions", x => new { x.PollId, x.Number });
                    table.CheckConstraint("CK_ConversationPollOptions_Value", "[Number] BETWEEN 1 AND 10 AND LEN(LTRIM(RTRIM([Text])))>0");
                    table.ForeignKey(
                        name: "FK_ConversationPollOptions_ConversationPolls_PollId",
                        column: x => x.PollId,
                        principalSchema: "messaging",
                        principalTable: "ConversationPolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationPollVotes",
                schema: "messaging",
                columns: table => new
                {
                    PollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsQuiz = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationPollVotes", x => new { x.PollId, x.UserId });
                    table.CheckConstraint("CK_ConversationPollVotes_State", "[Revision]>0 AND ([IsQuiz]=0 OR [Revision]=1) AND [UpdatedAtUtc]>=[CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ConversationPollVotes_ConversationPolls_PollId_IsQuiz",
                        columns: x => new { x.PollId, x.IsQuiz },
                        principalSchema: "messaging",
                        principalTable: "ConversationPolls",
                        principalColumns: new[] { "Id", "IsQuiz" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationPollVotes_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationQuizAnswers",
                schema: "messaging",
                columns: table => new
                {
                    PollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsQuiz = table.Column<bool>(type: "bit", nullable: false),
                    CorrectOptionNumber = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationQuizAnswers", x => x.PollId);
                    table.CheckConstraint("CK_ConversationQuizAnswers_IsQuiz", "[IsQuiz]=1");
                    table.ForeignKey(
                        name: "FK_ConversationQuizAnswers_ConversationPollOptions_PollId_CorrectOptionNumber",
                        columns: x => new { x.PollId, x.CorrectOptionNumber },
                        principalSchema: "messaging",
                        principalTable: "ConversationPollOptions",
                        principalColumns: new[] { "PollId", "Number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationQuizAnswers_ConversationPolls_PollId_IsQuiz",
                        columns: x => new { x.PollId, x.IsQuiz },
                        principalSchema: "messaging",
                        principalTable: "ConversationPolls",
                        principalColumns: new[] { "Id", "IsQuiz" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationPollSelections",
                schema: "messaging",
                columns: table => new
                {
                    PollId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationPollSelections", x => new { x.PollId, x.UserId, x.OptionNumber });
                    table.ForeignKey(
                        name: "FK_ConversationPollSelections_ConversationPollOptions_PollId_OptionNumber",
                        columns: x => new { x.PollId, x.OptionNumber },
                        principalSchema: "messaging",
                        principalTable: "ConversationPollOptions",
                        principalColumns: new[] { "PollId", "Number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationPollSelections_ConversationPollVotes_PollId_UserId",
                        columns: x => new { x.PollId, x.UserId },
                        principalSchema: "messaging",
                        principalTable: "ConversationPollVotes",
                        principalColumns: new[] { "PollId", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPolls_ClosedByUserId",
                schema: "messaging",
                table: "ConversationPolls",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPolls_ConversationId_CreatedAtUtc_Id",
                schema: "messaging",
                table: "ConversationPolls",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPolls_CreatorUserId",
                schema: "messaging",
                table: "ConversationPolls",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPolls_Id_ConversationId",
                schema: "messaging",
                table: "ConversationPolls",
                columns: new[] { "Id", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPollSelections_PollId_OptionNumber_UserId",
                schema: "messaging",
                table: "ConversationPollSelections",
                columns: new[] { "PollId", "OptionNumber", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPollVotes_PollId_CreatedAtUtc_UserId",
                schema: "messaging",
                table: "ConversationPollVotes",
                columns: new[] { "PollId", "CreatedAtUtc", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPollVotes_PollId_IsQuiz",
                schema: "messaging",
                table: "ConversationPollVotes",
                columns: new[] { "PollId", "IsQuiz" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationPollVotes_UserId",
                schema: "messaging",
                table: "ConversationPollVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationQuizAnswers_PollId_CorrectOptionNumber",
                schema: "messaging",
                table: "ConversationQuizAnswers",
                columns: new[] { "PollId", "CorrectOptionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationQuizAnswers_PollId_IsQuiz",
                schema: "messaging",
                table: "ConversationQuizAnswers",
                columns: new[] { "PollId", "IsQuiz" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // بازگشت schema نباید دادهٔ رأی‌گیری یا شواهد moderation تازه را حذف کند.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [messaging].[ConversationPolls])
                    OR EXISTS (SELECT 1 FROM [messaging].[Messages] WHERE [Kind] = 7)
                    OR EXISTS (SELECT 1 FROM [messaging].[ChangeEvents] WHERE [ChangeType] = 14)
                    OR EXISTS (SELECT 1 FROM [messaging].[AbuseReports] WHERE [ReportedMessageKind] IN (6, 7))
                    OR EXISTS (SELECT 1 FROM [messaging].[SafetyIncidents] WHERE [ContentKind] IN (6, 7))
                    THROW 51000, 'Poll/Quiz or moderation data exists; downgrade requires an explicit preservation plan.', 1;
                """);
            migrationBuilder.DropTable(
                name: "ConversationPollSelections",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationQuizAnswers",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationPollVotes",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationPollOptions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationPolls",
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
                sql: "[ContentKind] IN (1, 2, 3, 4, 5)");

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
                name: "CK_ChangeEvents_Type",
                schema: "messaging",
                table: "ChangeEvents",
                sql: "[ChangeType] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AbuseReports_MessageKind",
                schema: "messaging",
                table: "AbuseReports",
                sql: "[ReportedMessageKind] IN (1, 2, 3, 4, 5)");
        }
    }
}
