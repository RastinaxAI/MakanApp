using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformCommerceFinanceOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000018"), "Read plan and subscription operations", "platform.commerce.read" },
                    { new Guid("20000000-0000-0000-0000-000000000019"), "Manage plan and subscription operations", "platform.commerce.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000020"), "Read safe order and payment operations", "platform.finance.read" },
                    { new Guid("20000000-0000-0000-0000-000000000021"), "Manage provider-backed finance operations", "platform.finance.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000022"), "Read system and background work operations", "platform.operations.read" },
                    { new Guid("20000000-0000-0000-0000-000000000023"), "Manage approved system operations", "platform.operations.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000024"), "Read safe notification delivery operations", "platform.notifications.read" },
                    { new Guid("20000000-0000-0000-0000-000000000025"), "Read backend-owned AI operational state when integrated", "platform.ai.read" }
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000018"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000019"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000020"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000021"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000023"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000025"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000018"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000020"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000023"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000025"), new Guid("10000000-0000-0000-0000-000000000002") },
                    { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000018"), new Guid("10000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000019"), new Guid("10000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000020"), new Guid("10000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000021"), new Guid("10000000-0000-0000-0000-000000000004") },
                    { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000006") },
                    { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000006") }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000018"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000019"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000020"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000021"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000023"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000025"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000018"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000020"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000023"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000025"), new Guid("10000000-0000-0000-0000-000000000002") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000018"), new Guid("10000000-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000019"), new Guid("10000000-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000020"), new Guid("10000000-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000021"), new Guid("10000000-0000-0000-0000-000000000004") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000006") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000006") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000025"));
        }
    }
}
