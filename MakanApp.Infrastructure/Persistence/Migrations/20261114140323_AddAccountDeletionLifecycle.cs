using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountDeletionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountDeletionRequests_Policy",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymizedAtUtc",
                schema: "identity",
                table: "Users",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletionStartedAtUtc",
                schema: "identity",
                table: "Users",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CredentialId",
                schema: "identity",
                table: "PhoneChangeRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimToken",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedUntilUtc",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailureStep",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "account-deletion-v1");

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledExecutionAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "identity",
                table: "AccountDeletionRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AccountDeletionRequests_Id_UserId",
                schema: "identity",
                table: "AccountDeletionRequests",
                columns: new[] { "Id", "UserId" });

            migrationBuilder.CreateTable(
                name: "AccountDeletionKnowledgePurges",
                schema: "knowledge",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandRequestId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletionKnowledgePurges", x => new { x.RequestId, x.SourceId, x.Version });
                    table.CheckConstraint("CK_AccountDeletionKnowledgePurges_Attempts", "[AttemptCount]>=0 AND [Version]>0");
                    table.CheckConstraint("CK_AccountDeletionKnowledgePurges_Outcome", "([CompletedAtUtc] IS NULL AND [Outcome] IS NULL) OR ([CompletedAtUtc] IS NOT NULL AND [Outcome] IS NOT NULL AND [Outcome] IN (N'Deleted',N'NotIngested'))");
                    table.ForeignKey(
                        name: "FK_AccountDeletionKnowledgePurges_AccountDeletionRequests_RequestId_OwnerUserId",
                        columns: x => new { x.RequestId, x.OwnerUserId },
                        principalSchema: "identity",
                        principalTable: "AccountDeletionRequests",
                        principalColumns: new[] { "Id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountDeletionKnowledgePurges_EducationalSourceVersions_SourceId_Version",
                        columns: x => new { x.SourceId, x.Version },
                        principalSchema: "knowledge",
                        principalTable: "EducationalSourceVersions",
                        principalColumns: new[] { "SourceId", "Number" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountDeletionSteps",
                schema: "identity",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Step = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountDeletionSteps", x => new { x.RequestId, x.Step });
                    table.CheckConstraint("CK_AccountDeletionSteps_Attempts", "[AttemptCount]>=0");
                    table.CheckConstraint("CK_AccountDeletionSteps_Completed", "([Status]=3 AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [CompletedAtUtc]>=[StartedAtUtc]) OR ([Status]<>3 AND [CompletedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_AccountDeletionSteps_Status", "[Status] IN (1,2,3,4)");
                    table.CheckConstraint("CK_AccountDeletionSteps_Step", "[Step] BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_AccountDeletionSteps_AccountDeletionRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "identity",
                        principalTable: "AccountDeletionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // رسیدهای قبلی صرفاً Requested هستند؛ هشت checkpoint خالی ایجاد می‌شود و هیچ حسابی در migration حذف نمی‌شود.
            migrationBuilder.Sql("""
                INSERT INTO [identity].[AccountDeletionSteps]
                    ([RequestId],[Step],[Status],[AttemptCount])
                SELECT r.[Id], s.[Step], 1, 0 FROM [identity].[AccountDeletionRequests] r
                CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8)) s([Step]);
                """);

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000035"), "ادامهٔ کنترل‌شدهٔ اجرای درخواست حذف حساب", "platform.privacy_requests.manage" });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000035"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Anonymized",
                schema: "identity",
                table: "Users",
                sql: "[AnonymizedAtUtc] IS NULL OR ([DeletionStartedAtUtc] IS NOT NULL AND [AnonymizedAtUtc]>=[DeletionStartedAtUtc] AND [Bio] IS NULL AND [ProfilePhotoFileAssetId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Deletion",
                schema: "identity",
                table: "Users",
                sql: "[DeletionStartedAtUtc] IS NULL OR [AccountStatus]=2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PhoneChangeRequests_DetachedCredential",
                schema: "identity",
                table: "PhoneChangeRequests",
                sql: "[CredentialId] IS NOT NULL OR [State] IN (3,4)");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionRequests_Status_ScheduledExecutionAtUtc_ClaimedUntilUtc_NextAttemptAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests",
                columns: new[] { "Status", "ScheduledExecutionAtUtc", "ClaimedUntilUtc", "NextAttemptAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountDeletionRequests_Attempts",
                schema: "identity",
                table: "AccountDeletionRequests",
                sql: "[AttemptCount]>=0 AND [ConsecutiveFailures]>=0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountDeletionRequests_Claim",
                schema: "identity",
                table: "AccountDeletionRequests",
                sql: "([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL) OR ([Status]=3 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountDeletionRequests_Completed",
                schema: "identity",
                table: "AccountDeletionRequests",
                sql: "([Status]=5 AND [CompletedAtUtc] IS NOT NULL AND [StartedAtUtc] IS NOT NULL AND [CompletedAtUtc]>=[StartedAtUtc]) OR ([Status]<>5 AND [CompletedAtUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountDeletionRequests_Policy",
                schema: "identity",
                table: "AccountDeletionRequests",
                sql: "[PolicyStatus] IN (N'PRODUCT_LEGAL_DECISION_REQUIRED',N'EXECUTION_DISABLED',N'EXECUTION_SCHEDULED',N'EXECUTION_COMPLETED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountDeletionRequests_Schedule",
                schema: "identity",
                table: "AccountDeletionRequests",
                sql: "([Status]=1 AND [ScheduledExecutionAtUtc] IS NULL) OR ([Status]<>1 AND [ScheduledExecutionAtUtc] IS NOT NULL AND [ScheduledExecutionAtUtc]>=[RequestedAtUtc])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountDeletionRequests_Status",
                schema: "identity",
                table: "AccountDeletionRequests",
                sql: "[Status] IN (1,2,3,4,5)");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionKnowledgePurges_CommandRequestId",
                schema: "knowledge",
                table: "AccountDeletionKnowledgePurges",
                column: "CommandRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionKnowledgePurges_RequestId_OwnerUserId",
                schema: "knowledge",
                table: "AccountDeletionKnowledgePurges",
                columns: new[] { "RequestId", "OwnerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountDeletionKnowledgePurges_SourceId_Version",
                schema: "knowledge",
                table: "AccountDeletionKnowledgePurges",
                columns: new[] { "SourceId", "Version" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // اجرای حذف برگشت‌پذیر نیست؛ downgrade نباید Credential ساختگی یا ادعای بازگردانی هویت بسازد.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [identity].[Users] WHERE [DeletionStartedAtUtc] IS NOT NULL)
                  OR EXISTS (SELECT 1 FROM [identity].[PhoneChangeRequests] WHERE [CredentialId] IS NULL)
                    THROW 51000, N'پس از شروع حذف حساب، downgrade نیازمند برنامهٔ بازیابی مصوب است.', 1;
                """);
            migrationBuilder.DropTable(
                name: "AccountDeletionKnowledgePurges",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "AccountDeletionSteps",
                schema: "identity");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Anonymized",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Deletion",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PhoneChangeRequests_DetachedCredential",
                schema: "identity",
                table: "PhoneChangeRequests");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AccountDeletionRequests_Id_UserId",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropIndex(
                name: "IX_AccountDeletionRequests_Status_ScheduledExecutionAtUtc_ClaimedUntilUtc_NextAttemptAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountDeletionRequests_Attempts",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountDeletionRequests_Claim",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountDeletionRequests_Completed",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountDeletionRequests_Policy",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountDeletionRequests_Schedule",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountDeletionRequests_Status",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000035"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000035"));

            migrationBuilder.DropColumn(
                name: "AnonymizedAtUtc",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletionStartedAtUtc",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "ClaimToken",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "ClaimedUntilUtc",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "FailureStep",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "LastAttemptAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "ScheduledExecutionAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "identity",
                table: "AccountDeletionRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "CredentialId",
                schema: "identity",
                table: "PhoneChangeRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountDeletionRequests_Policy",
                schema: "identity",
                table: "AccountDeletionRequests",
                sql: "[PolicyStatus] = N'PRODUCT_LEGAL_DECISION_REQUIRED'");
        }
    }
}
