using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScopedEducationalSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.CreateTable(
                name: "EducationalSources",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationRequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    PublishedVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationalSources", x => x.Id);
                    table.CheckConstraint("CK_EducationalSources_Owner", "([Scope]=1 AND [OwnerUserId] IS NOT NULL AND [OrganizationId] IS NULL AND [ClassId] IS NULL) OR ([Scope]=2 AND [OwnerUserId] IS NULL AND [OrganizationId] IS NOT NULL AND [ClassId] IS NOT NULL) OR ([Scope]=3 AND [OwnerUserId] IS NULL AND [OrganizationId] IS NOT NULL AND [ClassId] IS NULL) OR ([Scope]=4 AND [OwnerUserId] IS NULL AND [OrganizationId] IS NULL AND [ClassId] IS NULL)");
                    table.CheckConstraint("CK_EducationalSources_State", "[Status] IN (1,2,3) AND [Visibility] IN (1,2) AND ([Scope]=4 OR [Visibility]=1) AND [CurrentVersion]>0 AND ([PublishedVersion] IS NULL OR ([PublishedVersion]>0 AND [PublishedVersion]<=[CurrentVersion])) AND (([Status]=3 AND [ArchivedAtUtc] IS NOT NULL) OR ([Status]<>3 AND [ArchivedAtUtc] IS NULL))");
                    table.ForeignKey(
                        name: "FK_EducationalSources_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EducationalSources_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EducationalSources_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EducationalSources_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EducationalSourceVersions",
                schema: "knowledge",
                columns: table => new
                {
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    PublicationStatus = table.Column<int>(type: "int", nullable: false),
                    SafetyStatus = table.Column<int>(type: "int", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "int", nullable: false),
                    ProcessingFailureCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EducationalSourceVersions", x => new { x.SourceId, x.Number });
                    table.CheckConstraint("CK_EducationalSourceVersions_State", "[Number]>0 AND [SourceType] IN (1,2) AND [SafetyStatus] IN (1,2,3,4) AND [ProcessingStatus] IN (1,2,3,4,5) AND (([PublicationStatus]=1 AND [PublishedAtUtc] IS NULL) OR ([PublicationStatus]=2 AND [PublishedAtUtc] IS NOT NULL)) AND (([ProcessingStatus]=5 AND [ProcessingFailureCode]=N'SOURCE_PROCESSING_FAILED') OR ([ProcessingStatus]<>5 AND [ProcessingFailureCode] IS NULL))");
                    table.ForeignKey(
                        name: "FK_EducationalSourceVersions_EducationalSources_SourceId",
                        column: x => x.SourceId,
                        principalSchema: "knowledge",
                        principalTable: "EducationalSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EducationalSourceVersions_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EducationalSourceVersions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000026"), "مدیریت منابع کتابخانهٔ ماکان", "platform.library.sources.manage" });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000026"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "IX_EducationalSources_CreatedByUserId_ClientOperationId",
                schema: "knowledge",
                table: "EducationalSources",
                columns: new[] { "CreatedByUserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EducationalSources_OrganizationId_ClassId_CreatedAtUtc",
                schema: "knowledge",
                table: "EducationalSources",
                columns: new[] { "OrganizationId", "ClassId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EducationalSources_OwnerUserId_CreatedAtUtc",
                schema: "knowledge",
                table: "EducationalSources",
                columns: new[] { "OwnerUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EducationalSourceVersions_CreatedByUserId",
                schema: "knowledge",
                table: "EducationalSourceVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EducationalSourceVersions_FileAssetId",
                schema: "knowledge",
                table: "EducationalSourceVersions",
                column: "FileAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // بازگشت پس از استفاده از منابع نیازمند برنامهٔ مستقل حفظ تاریخچه است.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [knowledge].[EducationalSources]) THROW 51132, N'بازگشت schema منابع استفاده‌شده نیازمند تأیید مستقل حفظ داده است.', 1;");
            migrationBuilder.DropTable(
                name: "EducationalSourceVersions",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "EducationalSources",
                schema: "knowledge");

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000026"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000026"));
        }
    }
}
