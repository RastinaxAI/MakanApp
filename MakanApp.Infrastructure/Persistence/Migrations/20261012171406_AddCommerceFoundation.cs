using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommerceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "commerce");

            migrationBuilder.CreateTable(
                name: "Plans",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanVersions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanVersions", x => x.Id);
                    table.CheckConstraint("CK_PlanVersions_Publication", "([Status] = 1 AND [PublishedAtUtc] IS NULL) OR ([Status] = 2 AND [PublishedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_PlanVersions_Status", "[Status] IN (1, 2)");
                    table.CheckConstraint("CK_PlanVersions_VersionNumber", "[VersionNumber] > 0");
                    table.ForeignKey(
                        name: "FK_PlanVersions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "commerce",
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanEntitlements",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanEntitlements", x => x.Id);
                    table.CheckConstraint("CK_PlanEntitlements_Key", "[Key] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_PlanEntitlements_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanQuotaDefinitions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false),
                    Limit = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanQuotaDefinitions", x => x.Id);
                    table.CheckConstraint("CK_PlanQuotaDefinitions_Key", "[Key] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_PlanQuotaDefinitions_Limit", "[Limit] IS NULL OR [Limit] >= 0");
                    table.ForeignKey(
                        name: "FK_PlanQuotaDefinitions_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.UniqueConstraint("UQ_Subscriptions_Organization_Id", x => new { x.OrganizationId, x.Id });
                    table.CheckConstraint("CK_Subscriptions_Cancellation", "([Status] = 4 AND [CancelledAtUtc] IS NOT NULL) OR ([Status] <> 4 AND [CancelledAtUtc] IS NULL)");
                    table.CheckConstraint("CK_Subscriptions_Dates", "[EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]");
                    table.CheckConstraint("CK_Subscriptions_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_Subscriptions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscriptions_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageLedger",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotaKey = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    SourceOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageLedger", x => x.Id);
                    table.CheckConstraint("CK_UsageLedger_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_UsageLedger_QuotaKey", "[QuotaKey] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_UsageLedger_Subscriptions_OrganizationId_SubscriptionId",
                        columns: x => new { x.OrganizationId, x.SubscriptionId },
                        principalSchema: "commerce",
                        principalTable: "Subscriptions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_PlanEntitlements_Version_Key",
                schema: "commerce",
                table: "PlanEntitlements",
                columns: new[] { "PlanVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlanQuotaDefinitions_Version_Key",
                schema: "commerce",
                table: "PlanQuotaDefinitions",
                columns: new[] { "PlanVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Plans_Code",
                schema: "commerce",
                table: "Plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PlanVersions_Plan_VersionNumber",
                schema: "commerce",
                table: "PlanVersions",
                columns: new[] { "PlanId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Organization_CreatedAt",
                schema: "commerce",
                table: "Subscriptions",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanVersionId",
                schema: "commerce",
                table: "Subscriptions",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_Organization_Active",
                schema: "commerce",
                table: "Subscriptions",
                column: "OrganizationId",
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_Organization_Subscription_Quota_OccurredAt",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "OrganizationId", "SubscriptionId", "QuotaKey", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_UsageLedger_Subscription_Quota_SourceOperation",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "SubscriptionId", "QuotaKey", "SourceOperationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanEntitlements",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PlanQuotaDefinitions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "UsageLedger",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "Subscriptions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PlanVersions",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "Plans",
                schema: "commerce");
        }
    }
}
