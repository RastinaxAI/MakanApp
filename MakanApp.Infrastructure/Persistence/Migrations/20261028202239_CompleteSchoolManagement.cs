using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSchoolManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Invitations_Pending_Destination_Organization_Role",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invitations_PersonLink",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedGuardianRelationId",
                schema: "organization",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GuardianLearnerOrganizationPersonId",
                schema: "organization",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassLessons",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationRequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLessons", x => x.Id);
                    table.UniqueConstraint("AK_ClassLessons_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_ClassLessons_Content", "LEN([Title]) BETWEEN 1 AND 200 AND LEN([Body]) <= 50000 AND [DisplayOrder] BETWEEN 0 AND 100000 AND [RevisionNumber] > 0");
                    table.CheckConstraint("CK_ClassLessons_Publication", "([Status]=1 AND [PublishedAtUtc] IS NULL) OR ([Status]=2 AND [PublishedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ClassLessons_Status", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_ClassLessons_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassLessons_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassLessonAttachments",
                schema: "academic",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassLessonAttachments", x => new { x.LessonId, x.FileAssetId });
                    table.CheckConstraint("CK_ClassLessonAttachments_Order", "[DisplayOrder] BETWEEN 0 AND 9");
                    table.ForeignKey(
                        name: "FK_ClassLessonAttachments_ClassLessons_OrganizationId_LessonId",
                        columns: x => new { x.OrganizationId, x.LessonId },
                        principalSchema: "academic",
                        principalTable: "ClassLessons",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassLessonAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_AcceptedGuardianRelationId",
                schema: "organization",
                table: "Invitations",
                column: "AcceptedGuardianRelationId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OrganizationId_GuardianLearnerOrganizationPersonId",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "OrganizationId", "GuardianLearnerOrganizationPersonId" });

            migrationBuilder.CreateIndex(
                name: "UX_Invitations_Pending_Destination_Organization_Role",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "DestinationUserId", "OrganizationId", "Role" },
                unique: true,
                filter: "[Status] = 1 AND [GuardianLearnerOrganizationPersonId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Invitations_Pending_Guardian_Learner",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "OrganizationId", "DestinationUserId", "GuardianLearnerOrganizationPersonId" },
                unique: true,
                filter: "[Status] = 1 AND [GuardianLearnerOrganizationPersonId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invitations_GuardianReceipt",
                schema: "organization",
                table: "Invitations",
                sql: "([GuardianLearnerOrganizationPersonId] IS NULL AND [AcceptedGuardianRelationId] IS NULL) OR ([GuardianLearnerOrganizationPersonId] IS NOT NULL AND (([Status]=2 AND [AcceptedGuardianRelationId] IS NOT NULL) OR ([Status]<>2 AND [AcceptedGuardianRelationId] IS NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invitations_PersonLink",
                schema: "organization",
                table: "Invitations",
                sql: "([OrganizationPersonId] IS NULL AND [GuardianLearnerOrganizationPersonId] IS NULL AND [ClientOperationId] IS NULL) OR ([OrganizationPersonId] IS NOT NULL AND [GuardianLearnerOrganizationPersonId] IS NULL AND [ClientOperationId] IS NOT NULL AND [Role] = 1) OR ([OrganizationPersonId] IS NULL AND [GuardianLearnerOrganizationPersonId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [Role] = 3)");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonAttachments_FileAssetId",
                schema: "academic",
                table: "ClassLessonAttachments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessonAttachments_OrganizationId_LessonId",
                schema: "academic",
                table: "ClassLessonAttachments",
                columns: new[] { "OrganizationId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessons_CreatedByUserId",
                schema: "academic",
                table: "ClassLessons",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessons_OrganizationId_ClassId_Status_DisplayOrder",
                schema: "academic",
                table: "ClassLessons",
                columns: new[] { "OrganizationId", "ClassId", "Status", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassLessons_OrganizationId_CreatedByUserId_ClientOperationId",
                schema: "academic",
                table: "ClassLessons",
                columns: new[] { "OrganizationId", "CreatedByUserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_GuardianRelations_AcceptedGuardianRelationId",
                schema: "organization",
                table: "Invitations",
                column: "AcceptedGuardianRelationId",
                principalSchema: "guardian",
                principalTable: "GuardianRelations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_OrganizationPersons_OrganizationId_GuardianLearnerOrganizationPersonId",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "OrganizationId", "GuardianLearnerOrganizationPersonId" },
                principalSchema: "organization",
                principalTable: "OrganizationPersons",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_GuardianRelations_AcceptedGuardianRelationId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_OrganizationPersons_OrganizationId_GuardianLearnerOrganizationPersonId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropTable(
                name: "ClassLessonAttachments",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "ClassLessons",
                schema: "academic");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_AcceptedGuardianRelationId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_OrganizationId_GuardianLearnerOrganizationPersonId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "UX_Invitations_Pending_Destination_Organization_Role",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "UX_Invitations_Pending_Guardian_Learner",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invitations_GuardianReceipt",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invitations_PersonLink",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "AcceptedGuardianRelationId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "GuardianLearnerOrganizationPersonId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "UX_Invitations_Pending_Destination_Organization_Role",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "DestinationUserId", "OrganizationId", "Role" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invitations_PersonLink",
                schema: "organization",
                table: "Invitations",
                sql: "([OrganizationPersonId] IS NULL AND [ClientOperationId] IS NULL) OR ([OrganizationPersonId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [Role] = 1)");
        }
    }
}
