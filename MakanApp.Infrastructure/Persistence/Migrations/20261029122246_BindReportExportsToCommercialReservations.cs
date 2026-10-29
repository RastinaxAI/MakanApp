using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BindReportExportsToCommercialReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_Reservation",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientOperationId",
                schema: "reports",
                table: "ReportExportJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommerceReservationId",
                schema: "reports",
                table: "ReportExportJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommercialOwnerType",
                schema: "reports",
                table: "ReportExportJobs",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<byte[]>(
                name: "RequestPayloadHash",
                schema: "reports",
                table: "ReportExportJobs",
                type: "binary(32)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_Reservation",
                schema: "commerce",
                table: "UsageLedger",
                sql: "[ReservationId] IS NULL OR ([ConsumerUserId] IS NOT NULL AND [ContextType] IS NOT NULL AND [ContextId] IS NOT NULL AND [PeriodStartsAtUtc] IS NOT NULL AND [PeriodEndsAtUtc] IS NOT NULL AND [PeriodEndsAtUtc]>[PeriodStartsAtUtc])");

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_CommercialOwnerType_OrganizationId_RequestedByUserId_CommerceReservationId",
                schema: "reports",
                table: "ReportExportJobs",
                columns: new[] { "CommercialOwnerType", "OrganizationId", "RequestedByUserId", "CommerceReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportExportJobs_RequestedByUserId_ClientOperationId",
                schema: "reports",
                table: "ReportExportJobs",
                columns: new[] { "RequestedByUserId", "ClientOperationId" },
                unique: true,
                filter: "[ClientOperationId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportExportJobs_CommercialOwner",
                schema: "reports",
                table: "ReportExportJobs",
                sql: "[CommercialOwnerType]=2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ReportExportJobs_Idempotency",
                schema: "reports",
                table: "ReportExportJobs",
                sql: "([ClientOperationId] IS NULL AND [RequestPayloadHash] IS NULL) OR ([ClientOperationId] IS NOT NULL AND [RequestPayloadHash] IS NOT NULL AND DATALENGTH([RequestPayloadHash])=32)");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExportJobs_UsageReservations_CommercialOwnerType_OrganizationId_RequestedByUserId_CommerceReservationId",
                schema: "reports",
                table: "ReportExportJobs",
                columns: new[] { "CommercialOwnerType", "OrganizationId", "RequestedByUserId", "CommerceReservationId" },
                principalSchema: "commerce",
                principalTable: "UsageReservations",
                principalColumns: new[] { "OwnerType", "OwnerId", "ConsumerUserId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // حذف ارتباط Job با رزرو و کلید تکرار، تاریخچهٔ مصرف را غیرقابل پیگیری می‌کند.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [reports].[ReportExportJobs] WHERE [ClientOperationId] IS NOT NULL OR [CommerceReservationId] IS NOT NULL)
                    THROW 51000, N'برگشت این مهاجرت به برنامهٔ صریح حفظ سابقهٔ خروجی گزارش نیاز دارد.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_ReportExportJobs_UsageReservations_CommercialOwnerType_OrganizationId_RequestedByUserId_CommerceReservationId",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_Reservation",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropIndex(
                name: "IX_ReportExportJobs_CommercialOwnerType_OrganizationId_RequestedByUserId_CommerceReservationId",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropIndex(
                name: "IX_ReportExportJobs_RequestedByUserId_ClientOperationId",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportExportJobs_CommercialOwner",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ReportExportJobs_Idempotency",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "ClientOperationId",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "CommerceReservationId",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "CommercialOwnerType",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.DropColumn(
                name: "RequestPayloadHash",
                schema: "reports",
                table: "ReportExportJobs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_Reservation",
                schema: "commerce",
                table: "UsageLedger",
                sql: "[ReservationId] IS NULL OR ([ConsumerUserId] IS NOT NULL AND [ContextType] IS NOT NULL AND [ContextId] IS NOT NULL AND [PeriodStartsAtUtc] IS NOT NULL AND [PeriodEndsAtUtc]>[PeriodStartsAtUtc])");
        }
    }
}
