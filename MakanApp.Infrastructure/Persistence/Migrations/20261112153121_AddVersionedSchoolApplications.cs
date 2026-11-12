using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedSchoolApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchoolApplications",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    SchoolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LegalEntityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TeachingHistory = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    WorkSamples = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DegreeField = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StudentCapacity = table.Column<int>(type: "int", nullable: true),
                    TeacherCapacity = table.Column<int>(type: "int", nullable: true),
                    SubmittedPolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolApplications", x => x.Id);
                    table.CheckConstraint("CK_SchoolApplications_Kind", "[Kind] IN (1,2)");
                    table.CheckConstraint("CK_SchoolApplications_Status", "[Status] IN (1,2,3,4,5,6)");
                    table.CheckConstraint("CK_SchoolApplications_SubmissionPolicy", "[Status]=1 OR [SubmittedPolicyVersion] IS NOT NULL");
                    table.CheckConstraint("CK_SchoolApplications_Version", "[Revision] > 0 AND [Version] >= [Revision]");
                    table.ForeignKey(
                        name: "FK_SchoolApplications_Users_ApplicantUserId",
                        column: x => x.ApplicantUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolApplicationOperations",
                schema: "organization",
                columns: table => new
                {
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolApplicationOperations", x => new { x.ActorUserId, x.ClientOperationId });
                    table.ForeignKey(
                        name: "FK_SchoolApplicationOperations_SchoolApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "organization",
                        principalTable: "SchoolApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolApplicationOperations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolApplicationRevisions",
                schema: "organization",
                columns: table => new
                {
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    ProtectedSnapshot = table.Column<string>(type: "nvarchar(max)", maxLength: 131072, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolApplicationRevisions", x => new { x.ApplicationId, x.Number });
                    table.ForeignKey(
                        name: "FK_SchoolApplicationRevisions_SchoolApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "organization",
                        principalTable: "SchoolApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolApplicationDecisions",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApplicantFeedback = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolApplicationDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolApplicationDecisions_SchoolApplicationRevisions_ApplicationId_Revision",
                        columns: x => new { x.ApplicationId, x.Revision },
                        principalSchema: "organization",
                        principalTable: "SchoolApplicationRevisions",
                        principalColumns: new[] { "ApplicationId", "Number" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolApplicationDecisions_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolApplicationDocuments",
                schema: "organization",
                columns: table => new
                {
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolApplicationDocuments", x => new { x.ApplicationId, x.Revision, x.FileAssetId });
                    table.ForeignKey(
                        name: "FK_SchoolApplicationDocuments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolApplicationDocuments_SchoolApplicationRevisions_ApplicationId_Revision",
                        columns: x => new { x.ApplicationId, x.Revision },
                        principalSchema: "organization",
                        principalTable: "SchoolApplicationRevisions",
                        principalColumns: new[] { "ApplicationId", "Number" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationDecisions_ApplicationId_OccurredAtUtc",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                columns: new[] { "ApplicationId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationDecisions_ApplicationId_Revision",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                columns: new[] { "ApplicationId", "Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationDecisions_ReviewerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationDocuments_FileAssetId",
                schema: "organization",
                table: "SchoolApplicationDocuments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationOperations_ApplicationId",
                schema: "organization",
                table: "SchoolApplicationOperations",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplications_ApplicantUserId_UpdatedAtUtc",
                schema: "organization",
                table: "SchoolApplications",
                columns: new[] { "ApplicantUserId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplications_Status_UpdatedAtUtc",
                schema: "organization",
                table: "SchoolApplications",
                columns: new[] { "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolApplicationDecisions",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "SchoolApplicationDocuments",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "SchoolApplicationOperations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "SchoolApplicationRevisions",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "SchoolApplications",
                schema: "organization");
        }
    }
}
