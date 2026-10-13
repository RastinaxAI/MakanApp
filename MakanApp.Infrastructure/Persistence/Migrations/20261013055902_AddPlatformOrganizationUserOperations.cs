using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformOrganizationUserOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountStatus",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAtUtc",
                schema: "identity",
                table: "Users",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE [identity].[Users] SET [AccountStatus] = 1 WHERE [AccountStatus] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "AccountStatus",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ProvisioningRequests",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitialManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubscriptionEndsAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    InvitationExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProvisioningRequests", x => x.Id);
                    table.CheckConstraint("CK_ProvisioningRequests_Status", "[Status] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_ProvisioningRequests_Invitations_ManagerInvitationId",
                        column: x => x.ManagerInvitationId,
                        principalSchema: "organization",
                        principalTable: "Invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvisioningRequests_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvisioningRequests_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvisioningRequests_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "commerce",
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvisioningRequests_Users_InitialManagerUserId",
                        column: x => x.InitialManagerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProvisioningRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000006"), "Read global organization operations", "platform.organizations.read" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), "Manage global organization operations", "platform.organizations.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), "Read safe global user operations", "platform.users.read" },
                    { new Guid("20000000-0000-0000-0000-000000000009"), "Manage global user account state", "platform.users.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000010"), "Revoke user sessions", "platform.sessions.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000011"), "Read organization provisioning state", "platform.provisioning.read" },
                    { new Guid("20000000-0000-0000-0000-000000000012"), "Manage organization provisioning", "platform.provisioning.manage" }
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000009"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000012"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000009"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000012"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000004") }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AccountStatus",
                schema: "identity",
                table: "Users",
                sql: "[AccountStatus] IN (1, 2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Suspension",
                schema: "identity",
                table: "Users",
                sql: "([AccountStatus] = 1 AND [SuspendedAtUtc] IS NULL) OR ([AccountStatus] = 2 AND [SuspendedAtUtc] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_InitialManagerUserId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "InitialManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_ManagerInvitationId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "ManagerInvitationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_OrganizationId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_PlanVersionId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_RequestedByUserId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_Status_UpdatedAtUtc",
                schema: "platform",
                table: "ProvisioningRequests",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_SubscriptionId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "UX_ProvisioningRequests_ClientOperationId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "ClientOperationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProvisioningRequests",
                schema: "platform");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AccountStatus",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Suspension",
                schema: "identity",
                table: "Users");

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000009"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000012"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000009"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000012"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"));

            migrationBuilder.DropColumn(
                name: "AccountStatus",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                schema: "identity",
                table: "Users");
        }
    }
}
