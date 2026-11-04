using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolMembershipManagementOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MembershipManagementOperations",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    RequestHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ResultRoleAssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipManagementOperations", x => x.Id);
                    table.CheckConstraint("CK_MembershipManagementOperations_Hash", "LEN([RequestHash]) = 64");
                    table.CheckConstraint("CK_MembershipManagementOperations_Kind", "[Kind] IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_MembershipManagementOperations_Result", "([Kind] = 4 AND [ResultRoleAssignmentId] IS NULL) OR ([Kind] IN (1, 2, 3) AND [ResultRoleAssignmentId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_MembershipManagementOperations_Memberships_OrganizationId_MembershipId",
                        columns: x => new { x.OrganizationId, x.MembershipId },
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipManagementOperations_RoleAssignments_ResultRoleAssignmentId",
                        column: x => x.ResultRoleAssignmentId,
                        principalSchema: "organization",
                        principalTable: "RoleAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MembershipManagementOperations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipManagementOperations_ActorUserId",
                schema: "organization",
                table: "MembershipManagementOperations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipManagementOperations_OrganizationId_MembershipId",
                schema: "organization",
                table: "MembershipManagementOperations",
                columns: new[] { "OrganizationId", "MembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipManagementOperations_ResultRoleAssignmentId",
                schema: "organization",
                table: "MembershipManagementOperations",
                column: "ResultRoleAssignmentId");

            migrationBuilder.CreateIndex(
                name: "UX_MembershipManagementOperations_Intent",
                schema: "organization",
                table: "MembershipManagementOperations",
                columns: new[] { "OrganizationId", "ActorUserId", "ClientOperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // حذف رسید استفاده‌شده تضمین Retry را از بین می‌برد؛ بازگشت به طرح صریح حفظ رسید نیاز دارد.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [organization].[MembershipManagementOperations]) THROW 51000, N'رسید مدیریت عضویت استفاده شده است؛ حذف آن نیازمند طرح حفظ Idempotency است.', 1;");
            migrationBuilder.DropTable(
                name: "MembershipManagementOperations",
                schema: "organization");
        }
    }
}
