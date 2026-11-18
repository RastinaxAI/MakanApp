using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUnmeteredAiOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageReservations_Trial",
                schema: "commerce",
                table: "UsageReservations");

            migrationBuilder.AddColumn<bool>(
                name: "IsUnmetered",
                schema: "commerce",
                table: "UsageReservations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnmetered",
                schema: "commerce",
                table: "UsageLedger",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageReservations_Trial",
                schema: "commerce",
                table: "UsageReservations",
                sql: "([IsUnmetered]=0 AND (([IsTrial]=1 AND [SubscriptionId] IS NULL AND [PlanVersionId] IS NULL AND [OwnerType]=1 AND [OwnerId]=[ConsumerUserId] AND [QuotaKey]=5 AND [Amount]=1) OR ([IsTrial]=0 AND [SubscriptionId] IS NOT NULL AND [PlanVersionId] IS NOT NULL))) OR ([IsUnmetered]=1 AND [IsTrial]=0 AND [SubscriptionId] IS NULL AND [PlanVersionId] IS NULL AND [OwnerType]=1 AND [OwnerId]=[ConsumerUserId] AND [QuotaKey]=5 AND [Amount]=1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_Unmetered",
                schema: "commerce",
                table: "UsageLedger",
                sql: "[IsUnmetered]=0 OR ([ReservationId] IS NOT NULL AND [SubscriptionId] IS NULL AND [PlanVersionId] IS NULL AND [OwnerType]=1 AND [OwnerId]=[ConsumerUserId] AND [ConsumerUserId] IS NOT NULL AND [QuotaKey]=5 AND [Amount]=1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // حذف علامت حسابداری، معنای عملیات ثبت‌شده را از بین می‌برد؛ rollback زیان‌بار مجاز نیست.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [commerce].[UsageReservations] WHERE [IsUnmetered]=1)
                    OR EXISTS (SELECT 1 FROM [commerce].[UsageLedger] WHERE [IsUnmetered]=1)
                    THROW 51000, 'UNMETERED_OPERATION_HISTORY_REQUIRES_FORWARD_RECOVERY', 1;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageReservations_Trial",
                schema: "commerce",
                table: "UsageReservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_Unmetered",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "IsUnmetered",
                schema: "commerce",
                table: "UsageReservations");

            migrationBuilder.DropColumn(
                name: "IsUnmetered",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageReservations_Trial",
                schema: "commerce",
                table: "UsageReservations",
                sql: "([IsTrial]=1 AND [SubscriptionId] IS NULL AND [PlanVersionId] IS NULL AND [OwnerType]=1 AND [OwnerId]=[ConsumerUserId] AND [QuotaKey]=5 AND [Amount]=1) OR ([IsTrial]=0 AND [SubscriptionId] IS NOT NULL AND [PlanVersionId] IS NOT NULL)");
        }
    }
}
