using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAsynchronousReportExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reports");

            migrationBuilder.CreateTable(
                name: "ReportExportJobs",
                schema: "reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    SubjectOrganizationPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    DataAsOfUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ToUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcademicPeriodId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnrollmentStatus = table.Column<int>(type: "int", nullable: true),
                    AssessmentKind = table.Column<int>(type: "int", nullable: true),
                    Sort = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    ExpectedRowCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    OutputFileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureCode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportExportJobs", x => x.Id);
                    table.CheckConstraint("CK_ReportExportJobs_AssessmentKind", "[AssessmentKind] IS NULL OR [AssessmentKind] IN (1, 2)");
                    table.CheckConstraint("CK_ReportExportJobs_AttemptCount", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_ReportExportJobs_Audience", "[Audience] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_ReportExportJobs_CompletedState", "([Status] = 3 AND [CompletedAtUtc] IS NOT NULL AND [OutputFileAssetId] IS NOT NULL AND [FailureCode] IS NULL) OR [Status] <> 3");
                    table.CheckConstraint("CK_ReportExportJobs_DateRange", "[FromUtc] IS NULL OR [ToUtc] IS NULL OR [FromUtc] <= [ToUtc]");
                    table.CheckConstraint("CK_ReportExportJobs_ExpectedRowCount", "[ExpectedRowCount] >= 0");
                    table.CheckConstraint("CK_ReportExportJobs_EnrollmentStatus", "[EnrollmentStatus] IS NULL OR [EnrollmentStatus] IN (1, 2, 3)");
                    table.CheckConstraint("CK_ReportExportJobs_FailedState", "([Status] = 4 AND [FailedAtUtc] IS NOT NULL AND [FailureCode] IS NOT NULL AND [OutputFileAssetId] IS NULL) OR [Status] <> 4");
                    table.CheckConstraint("CK_ReportExportJobs_Format", "[Format] = 1");
                    table.CheckConstraint("CK_ReportExportJobs_ProcessingLease", "([Status] = 2 AND [LeaseExpiresAtUtc] IS NOT NULL) OR [Status] <> 2");
                    table.CheckConstraint("CK_ReportExportJobs_ReportType", "[ReportType] BETWEEN 1 AND 14");
                    table.CheckConstraint("CK_ReportExportJobs_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_ReportExportJobs_FileAssets_OutputFileAssetId",
                        column: x => x.OutputFileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportExportJobs_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportExportJobs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportExportJobs_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_MembershipId",
                schema: "reports",
                table: "ReportExportJobs",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_OrganizationId",
                schema: "reports",
                table: "ReportExportJobs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_Owner_Organization_Requested",
                schema: "reports",
                table: "ReportExportJobs",
                columns: new[] { "RequestedByUserId", "OrganizationId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_Status_NextAttempt_Requested",
                schema: "reports",
                table: "ReportExportJobs",
                columns: new[] { "Status", "NextAttemptAtUtc", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_ReportExportJobs_OutputFileAsset",
                schema: "reports",
                table: "ReportExportJobs",
                column: "OutputFileAssetId",
                unique: true,
                filter: "[OutputFileAssetId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportExportJobs",
                schema: "reports");
        }
    }
}
