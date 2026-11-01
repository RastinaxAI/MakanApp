using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandMessagingAccountPolicyAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ClassId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicActivityPublications_Source",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.AddColumn<int>(
                name: "ActivityRevision",
                schema: "academic",
                table: "Sessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PhotoFileAssetId",
                schema: "messaging",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionAction",
                schema: "messaging",
                table: "AcademicActivityPublications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationInitialTargets",
                schema: "messaging",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvitationExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationInitialTargets", x => new { x.ConversationId, x.TargetUserId });
                    table.CheckConstraint("CK_ConversationInitialTargets_Outcome", "([Outcome]=2 AND [InvitationId] IS NOT NULL AND [InvitationExpiresAtUtc] IS NOT NULL) OR ([Outcome] IN (1,3) AND [InvitationId] IS NULL AND [InvitationExpiresAtUtc] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ConversationInitialTargets_ConversationInvitations_InvitationId",
                        column: x => x.InvitationId,
                        principalSchema: "messaging",
                        principalTable: "ConversationInvitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationInitialTargets_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Sessions_ActivityRevision",
                schema: "academic",
                table: "Sessions",
                sql: "[ActivityRevision]>=0");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_PhotoFileAssetId",
                schema: "messaging",
                table: "Conversations",
                column: "PhotoFileAssetId",
                unique: true,
                filter: "[PhotoFileAssetId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_PhotoType",
                schema: "messaging",
                table: "Conversations",
                sql: "[PhotoFileAssetId] IS NULL OR [Type] IN (2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ClassId_SessionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "ClassId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_SessionId_SourceRevision",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "SessionId", "SourceRevision" },
                unique: true,
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicActivityPublications_SessionAction",
                schema: "messaging",
                table: "AcademicActivityPublications",
                sql: "([Kind]=4 AND [SessionAction] IS NOT NULL) OR ([Kind]<>4 AND [SessionAction] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicActivityPublications_Source",
                schema: "messaging",
                table: "AcademicActivityPublications",
                sql: "([Kind]=1 AND [LessonId] IS NOT NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NULL AND [SessionId] IS NULL AND [SessionAction] IS NULL) OR ([Kind]=2 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NOT NULL AND [ExamVersionId] IS NULL AND [SessionId] IS NULL AND [SessionAction] IS NULL) OR ([Kind]=3 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NOT NULL AND [SessionId] IS NULL AND [SessionAction] IS NULL) OR ([Kind]=4 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NULL AND [SessionId] IS NOT NULL AND [SessionAction] IN (1,2,3,4))");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInitialTargets_InvitationId",
                schema: "messaging",
                table: "ConversationInitialTargets",
                column: "InvitationId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicActivityPublications_Sessions_OrganizationId_ClassId_SessionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "ClassId", "SessionId" },
                principalSchema: "academic",
                principalTable: "Sessions",
                principalColumns: new[] { "OrganizationId", "ClassId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_FileAssets_PhotoFileAssetId",
                schema: "messaging",
                table: "Conversations",
                column: "PhotoFileAssetId",
                principalSchema: "storage",
                principalTable: "FileAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicActivityPublications_Sessions_OrganizationId_ClassId_SessionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_FileAssets_PhotoFileAssetId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropTable(
                name: "ConversationInitialTargets",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Sessions_ActivityRevision",
                schema: "academic",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_PhotoFileAssetId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_PhotoType",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ClassId_SessionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropIndex(
                name: "IX_AcademicActivityPublications_SessionId_SourceRevision",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicActivityPublications_SessionAction",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AcademicActivityPublications_Source",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropColumn(
                name: "ActivityRevision",
                schema: "academic",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "PhotoFileAssetId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SessionAction",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "messaging",
                table: "AcademicActivityPublications");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ClassId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "ClassId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AcademicActivityPublications_Source",
                schema: "messaging",
                table: "AcademicActivityPublications",
                sql: "([Kind]=1 AND [LessonId] IS NOT NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NULL) OR ([Kind]=2 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NOT NULL AND [ExamVersionId] IS NULL) OR ([Kind]=3 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NOT NULL)");
        }
    }
}
