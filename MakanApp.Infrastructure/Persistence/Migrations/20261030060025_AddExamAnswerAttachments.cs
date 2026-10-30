using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAnswerAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedAnswerContentTypes",
                schema: "assessment",
                table: "QuestionVersions",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AnswerTextRequired",
                schema: "assessment",
                table: "QuestionVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AttachmentsAllowed",
                schema: "assessment",
                table: "QuestionVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MaximumAnswerAttachmentBytes",
                schema: "assessment",
                table: "QuestionVersions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumAnswerAttachmentCount",
                schema: "assessment",
                table: "QuestionVersions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnswerRevisionAttachments",
                schema: "assessment",
                columns: table => new
                {
                    AnswerRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamAttemptQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnswerRevisionAttachments", x => new { x.AnswerRevisionId, x.FileAssetId });
                    table.ForeignKey(
                        name: "FK_AnswerRevisionAttachments_AnswerRevisions_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                        columns: x => new { x.OrganizationId, x.ExamAttemptId, x.ExamAttemptQuestionId, x.AnswerRevisionId },
                        principalSchema: "assessment",
                        principalTable: "AnswerRevisions",
                        principalColumns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnswerRevisionAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_QuestionVersions_AttachmentPolicy",
                schema: "assessment",
                table: "QuestionVersions",
                sql: "([AttachmentsAllowed] = 0 AND [AnswerTextRequired] = 0 AND [MaximumAnswerAttachmentCount] IS NULL AND [MaximumAnswerAttachmentBytes] IS NULL AND [AllowedAnswerContentTypes] IS NULL) OR ([AttachmentsAllowed] = 1 AND [MaximumAnswerAttachmentCount] IS NOT NULL AND [MaximumAnswerAttachmentCount] > 0 AND [MaximumAnswerAttachmentBytes] IS NOT NULL AND [MaximumAnswerAttachmentBytes] > 0 AND [AllowedAnswerContentTypes] IS NOT NULL AND LEN([AllowedAnswerContentTypes]) > 0 AND ([Type] = 2 OR [AnswerTextRequired] = 0))");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_FileAssetId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_OrganizationId_ExamAttemptId_ExamAttemptQuestionId_AnswerRevisionId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "OrganizationId", "ExamAttemptId", "ExamAttemptQuestionId", "AnswerRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AnswerRevisionAttachments_OrganizationId_ExamAttemptId_FileAssetId",
                schema: "assessment",
                table: "AnswerRevisionAttachments",
                columns: new[] { "OrganizationId", "ExamAttemptId", "FileAssetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnswerRevisionAttachments",
                schema: "assessment");

            migrationBuilder.DropCheckConstraint(
                name: "CK_QuestionVersions_AttachmentPolicy",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "AllowedAnswerContentTypes",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "AnswerTextRequired",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "AttachmentsAllowed",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "MaximumAnswerAttachmentBytes",
                schema: "assessment",
                table: "QuestionVersions");

            migrationBuilder.DropColumn(
                name: "MaximumAnswerAttachmentCount",
                schema: "assessment",
                table: "QuestionVersions");
        }
    }
}
