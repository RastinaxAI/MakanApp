using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingMediaReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationCases_Source",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationCases_SourceShape",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions");

            migrationBuilder.AddColumn<string>(
                name: "MediaContentType",
                schema: "messaging",
                table: "ModerationCases",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaDecision",
                schema: "messaging",
                table: "ModerationCases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaFileAssetId",
                schema: "messaging",
                table: "ModerationCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MediaReviewedAtUtc",
                schema: "messaging",
                table: "ModerationCases",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaReviewedByUserId",
                schema: "messaging",
                table: "ModerationCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaSha256Hash",
                schema: "messaging",
                table: "ModerationCases",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MediaSizeBytes",
                schema: "messaging",
                table: "ModerationCases",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_MediaReviewedByUserId",
                schema: "messaging",
                table: "ModerationCases",
                column: "MediaReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_ModerationCases_MediaFileAssetId",
                schema: "messaging",
                table: "ModerationCases",
                column: "MediaFileAssetId",
                unique: true,
                filter: "[MediaFileAssetId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationCases_MediaReview",
                schema: "messaging",
                table: "ModerationCases",
                sql: "([Source] <> 3 AND [MediaSha256Hash] IS NULL AND [MediaContentType] IS NULL AND [MediaSizeBytes] IS NULL AND [MediaDecision] IS NULL AND [MediaReviewedByUserId] IS NULL AND [MediaReviewedAtUtc] IS NULL) OR ([Source] = 3 AND [QueueScope] = 2 AND [MediaSha256Hash] IS NOT NULL AND LEN([MediaSha256Hash]) = 64 AND [MediaContentType] IS NOT NULL AND [MediaSizeBytes] IS NOT NULL AND [MediaSizeBytes] > 0 AND [MediaDecision] IS NOT NULL AND (([MediaDecision] = 3 AND [Status] IN (1, 2) AND [MediaReviewedByUserId] IS NULL AND [MediaReviewedAtUtc] IS NULL) OR ([MediaDecision] IN (1, 2) AND [Status] IN (2, 3, 4) AND ([Status] <> 4 OR [MediaDecision] = 2) AND [MediaReviewedByUserId] IS NOT NULL AND [MediaReviewedAtUtc] IS NOT NULL AND [MediaReviewedAtUtc] >= [CreatedAtUtc])))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationCases_Source",
                schema: "messaging",
                table: "ModerationCases",
                sql: "[Source] IN (1, 2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationCases_SourceShape",
                schema: "messaging",
                table: "ModerationCases",
                sql: "([Source] = 1 AND [AbuseReportId] IS NOT NULL AND [SafetyIncidentId] IS NULL AND [MediaFileAssetId] IS NULL) OR ([Source] = 2 AND [AbuseReportId] IS NULL AND [SafetyIncidentId] IS NOT NULL AND [MediaFileAssetId] IS NULL) OR ([Source] = 3 AND [AbuseReportId] IS NULL AND [SafetyIncidentId] IS NULL AND [MediaFileAssetId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationCases_FileAssets_MediaFileAssetId",
                schema: "messaging",
                table: "ModerationCases",
                column: "MediaFileAssetId",
                principalSchema: "storage",
                principalTable: "FileAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModerationCases_Users_MediaReviewedByUserId",
                schema: "messaging",
                table: "ModerationCases",
                column: "MediaReviewedByUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModerationCases_FileAssets_MediaFileAssetId",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropForeignKey(
                name: "FK_ModerationCases_Users_MediaReviewedByUserId",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropIndex(
                name: "IX_ModerationCases_MediaReviewedByUserId",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropIndex(
                name: "UX_ModerationCases_MediaFileAssetId",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationCases_MediaReview",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationCases_Source",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationCases_SourceShape",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions");

            migrationBuilder.DropColumn(
                name: "MediaContentType",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "MediaDecision",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "MediaFileAssetId",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "MediaReviewedAtUtc",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "MediaReviewedByUserId",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "MediaSha256Hash",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "MediaSizeBytes",
                schema: "messaging",
                table: "ModerationCases");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationCases_Source",
                schema: "messaging",
                table: "ModerationCases",
                sql: "[Source] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationCases_SourceShape",
                schema: "messaging",
                table: "ModerationCases",
                sql: "([Source] = 1 AND [AbuseReportId] IS NOT NULL AND [SafetyIncidentId] IS NULL) OR ([Source] = 2 AND [AbuseReportId] IS NULL AND [SafetyIncidentId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8)");
        }
    }
}
