using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformIamAndAdminAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false),
                    BeforeSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AfterSummary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AdminAuditLogs_Result", "[Result] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_Persons_ActorPersonId",
                        column: x => x.ActorPersonId,
                        principalSchema: "identity",
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_UserSessions_ActorSessionId",
                        column: x => x.ActorSessionId,
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformPermissions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRoles",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRoleAssignments",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    EndedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRoleAssignments", x => x.Id);
                    table.CheckConstraint("CK_PlatformRoleAssignments_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_PlatformRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "platform",
                        principalTable: "PlatformRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_Users_EndedByUserId",
                        column: x => x.EndedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformRoleAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformRolePermissions",
                schema: "platform",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformRolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_PlatformRolePermissions_PlatformPermissions_PermissionId",
                        column: x => x.PermissionId,
                        principalSchema: "platform",
                        principalTable: "PlatformPermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlatformRolePermissions_PlatformRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "platform",
                        principalTable: "PlatformRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "Access platform backoffice", "platform.access" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "Read platform role catalog and assignments", "platform.roles.read" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "Manage platform role assignments", "platform.roles.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "Read platform administration audit", "platform.audit.read" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "Read platform security metadata", "platform.security.read" }
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRoles",
                columns: new[] { "Id", "DisplayName", "Key" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Platform Owner", "PlatformOwner" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Operations", "Operations" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Support", "Support" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Finance", "Finance" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Content", "Content" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "Security Audit", "SecurityAudit" },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "Abuse Reviewer", "AbuseReviewer" }
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000005") },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000006") },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new Guid("10000000-0000-0000-0000-000000000006") },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new Guid("10000000-0000-0000-0000-000000000006") },
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000007") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_Action_OccurredAtUtc",
                schema: "platform",
                table: "AdminAuditLogs",
                columns: new[] { "Action", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_Actor_OccurredAtUtc",
                schema: "platform",
                table: "AdminAuditLogs",
                columns: new[] { "ActorUserId", "OccurredAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ActorPersonId",
                schema: "platform",
                table: "AdminAuditLogs",
                column: "ActorPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ActorSessionId",
                schema: "platform",
                table: "AdminAuditLogs",
                column: "ActorSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_OccurredAtUtc",
                schema: "platform",
                table: "AdminAuditLogs",
                column: "OccurredAtUtc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_OrganizationId",
                schema: "platform",
                table: "AdminAuditLogs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "UX_PlatformPermissions_Key",
                schema: "platform",
                table: "PlatformPermissions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRoleAssignments_EndedByUserId",
                schema: "platform",
                table: "PlatformRoleAssignments",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRoleAssignments_GrantedByUserId",
                schema: "platform",
                table: "PlatformRoleAssignments",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRoleAssignments_Role_Status",
                schema: "platform",
                table: "PlatformRoleAssignments",
                columns: new[] { "RoleId", "Status", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PlatformRoleAssignments_Active_User_Role",
                schema: "platform",
                table: "PlatformRoleAssignments",
                columns: new[] { "UserId", "RoleId" },
                unique: true,
                filter: "[Status] = 1 AND [EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformRolePermissions_PermissionId",
                schema: "platform",
                table: "PlatformRolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_PlatformRoles_Key",
                schema: "platform",
                table: "PlatformRoles",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformRoleAssignments",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformRolePermissions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformPermissions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "PlatformRoles",
                schema: "platform");
        }
    }
}
