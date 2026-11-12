using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportPrivacyAndAssistantContexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "intelligence");

            migrationBuilder.EnsureSchema(
                name: "support");

            migrationBuilder.CreateTable(
                name: "AccountDeletionRequests",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBySessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PolicyStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletionRequests", x => x.Id);
                    table.CheckConstraint("CK_AccountDeletionRequests_Policy", "[PolicyStatus] = N'PRODUCT_LEGAL_DECISION_REQUIRED'");
                    table.ForeignKey(
                        name: "FK_AccountDeletionRequests_UserSessions_RequestedBySessionId",
                        column: x => x.RequestedBySessionId,
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountDeletionRequests_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssistantContextSelections",
                schema: "intelligence",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<int>(type: "int", nullable: true),
                    SubjectOrganizationPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SelectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantContextSelections", x => x.SessionId);
                    table.CheckConstraint("CK_AssistantContextSelections_Kind", "[Kind] IN (1,2,3)");
                    table.CheckConstraint("CK_AssistantContextSelections_Scope", "([Kind]=1 AND [OrganizationId] IS NULL AND [MembershipId] IS NULL AND [Role] IS NULL AND [SubjectOrganizationPersonId] IS NULL AND [ClassId] IS NULL) OR ([Kind]=2 AND [OrganizationId] IS NOT NULL AND [MembershipId] IS NOT NULL AND [Role] IS NOT NULL AND [ClassId] IS NULL) OR ([Kind]=3 AND [OrganizationId] IS NOT NULL AND [MembershipId] IS NOT NULL AND [Role] IS NOT NULL AND [ClassId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AssistantContextSelections_Classes_ClassId",
                        column: x => x.ClassId,
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantContextSelections_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantContextSelections_OrganizationPersons_SubjectOrganizationPersonId",
                        column: x => x.SubjectOrganizationPersonId,
                        principalSchema: "organization",
                        principalTable: "OrganizationPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantContextSelections_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantContextSelections_UserSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "identity",
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssistantContextSelections_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedStaffUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.CheckConstraint("CK_SupportTickets_Resolution", "([Status]=5 AND [ResolvedAtUtc] IS NOT NULL) OR ([Status]<>5 AND [ResolvedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_SupportTickets_Status", "[Status] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_SupportTickets_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_AssignedStaffUserId",
                        column: x => x.AssignedStaffUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketMessages",
                schema: "support",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsStaff = table.Column<bool>(type: "bit", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    TicketVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketMessages", x => x.Id);
                    table.UniqueConstraint("AK_SupportTicketMessages_TicketId_Id", x => new { x.TicketId, x.Id });
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "support",
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketOperations",
                schema: "support",
                columns: table => new
                {
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ResultVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketOperations", x => new { x.ActorUserId, x.ClientOperationId });
                    table.ForeignKey(
                        name: "FK_SupportTicketOperations_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalSchema: "support",
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketOperations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketAttachments",
                schema: "support",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketAttachments", x => new { x.MessageId, x.FileAssetId });
                    table.ForeignKey(
                        name: "FK_SupportTicketAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTicketAttachments_SupportTicketMessages_TicketId_MessageId",
                        columns: x => new { x.TicketId, x.MessageId },
                        principalSchema: "support",
                        principalTable: "SupportTicketMessages",
                        principalColumns: new[] { "TicketId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000030"), "مشاهدهٔ درخواست تأسیس آموزشگاه", "platform.school_applications.read" },
                    { new Guid("20000000-0000-0000-0000-000000000031"), "بررسی درخواست تأسیس آموزشگاه", "platform.school_applications.review" },
                    { new Guid("20000000-0000-0000-0000-000000000032"), "مشاهدهٔ پرونده‌های پشتیبانی", "platform.support.read" },
                    { new Guid("20000000-0000-0000-0000-000000000033"), "پاسخ و مدیریت پروندهٔ پشتیبانی", "platform.support.manage" },
                    { new Guid("20000000-0000-0000-0000-000000000034"), "مشاهدهٔ وضعیت درخواست‌های حریم خصوصی", "platform.privacy_requests.read" }
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000030"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000031"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000032"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000033"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000034"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000032"), new Guid("10000000-0000-0000-0000-000000000003") },
                    { new Guid("20000000-0000-0000-0000-000000000033"), new Guid("10000000-0000-0000-0000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionRequests_RequestedBySessionId",
                schema: "identity",
                table: "AccountDeletionRequests",
                column: "RequestedBySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionRequests_UserId",
                schema: "identity",
                table: "AccountDeletionRequests",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionRequests_UserId_ClientOperationId",
                schema: "identity",
                table: "AccountDeletionRequests",
                columns: new[] { "UserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantContextSelections_ClassId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantContextSelections_MembershipId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantContextSelections_OrganizationId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantContextSelections_SubjectOrganizationPersonId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                column: "SubjectOrganizationPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantContextSelections_UserId",
                schema: "intelligence",
                table: "AssistantContextSelections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketAttachments_FileAssetId",
                schema: "support",
                table: "SupportTicketAttachments",
                column: "FileAssetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketAttachments_TicketId_MessageId",
                schema: "support",
                table: "SupportTicketAttachments",
                columns: new[] { "TicketId", "MessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_AuthorUserId",
                schema: "support",
                table: "SupportTicketMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_TicketId_TicketVersion",
                schema: "support",
                table: "SupportTicketMessages",
                columns: new[] { "TicketId", "TicketVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketOperations_TicketId",
                schema: "support",
                table: "SupportTicketOperations",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_AssignedStaffUserId",
                schema: "support",
                table: "SupportTickets",
                column: "AssignedStaffUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_OwnerUserId_UpdatedAtUtc",
                schema: "support",
                table: "SupportTickets",
                columns: new[] { "OwnerUserId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status_UpdatedAtUtc",
                schema: "support",
                table: "SupportTickets",
                columns: new[] { "Status", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountDeletionRequests",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "AssistantContextSelections",
                schema: "intelligence");

            migrationBuilder.DropTable(
                name: "SupportTicketAttachments",
                schema: "support");

            migrationBuilder.DropTable(
                name: "SupportTicketOperations",
                schema: "support");

            migrationBuilder.DropTable(
                name: "SupportTicketMessages",
                schema: "support");

            migrationBuilder.DropTable(
                name: "SupportTickets",
                schema: "support");

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000030"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000031"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000032"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000033"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000034"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000032"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000033"), new Guid("10000000-0000-0000-0000-000000000003") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000030"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000031"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000032"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000033"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000034"));
        }
    }
}
