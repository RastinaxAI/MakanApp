using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialOwnershipPeriodsAndPersonalClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsageLedger_Subscriptions_OrganizationId_SubscriptionId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropIndex(
                name: "UX_UsageLedger_Subscription_Quota_SourceOperation",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_QuotaKey",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Subscriptions_Organization_Id",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_Organization_Active",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_Status",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanVersionPrices_Amount",
                schema: "commerce",
                table: "PlanVersionPrices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanQuotaDefinitions_Key",
                schema: "commerce",
                table: "PlanQuotaDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanEntitlements_Key",
                schema: "commerce",
                table: "PlanEntitlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Amount",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubscriptionId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "ConsumerUserId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ContextId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContextType",
                schema: "commerce",
                table: "UsageLedger",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "OwnerType",
                schema: "commerce",
                table: "UsageLedger",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodEndsAtUtc",
                schema: "commerce",
                table: "UsageLedger",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PeriodStartsAtUtc",
                schema: "commerce",
                table: "UsageLedger",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PlanVersionId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "commerce",
                table: "UsageLedger",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReservationId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "commerce",
                table: "UsageLedger",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingAnchorUtc",
                schema: "commerce",
                table: "Subscriptions",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingPeriodIndex",
                schema: "commerce",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CommercialControlEnabled",
                schema: "commerce",
                table: "Subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "InitialPlanVersionId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "OwnerType",
                schema: "commerce",
                table: "Subscriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduledPlanVersionId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Calendar",
                schema: "commerce",
                table: "PlanVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CriticalWarningPercent",
                schema: "commerce",
                table: "PlanVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExplicitlyFree",
                schema: "commerce",
                table: "PlanVersions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SaleTermsReference",
                schema: "commerce",
                table: "PlanVersions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarningPercent",
                schema: "commerce",
                table: "PlanVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Audience",
                schema: "commerce",
                table: "Plans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailabilityEffectiveAtUtc",
                schema: "commerce",
                table: "Plans",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CatalogVisible",
                schema: "commerce",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NewPurchaseAvailable",
                schema: "commerce",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RenewalAvailable",
                schema: "commerce",
                table: "Plans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingAnchorUtc",
                schema: "commerce",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BillingPeriodIndex",
                schema: "commerce",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpectedPreviousPlanVersionId",
                schema: "commerce",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCommercialTerms",
                schema: "commerce",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                schema: "commerce",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetSubscriptionId",
                schema: "commerce",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsExpireAtUtc",
                schema: "commerce",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            // فقط واقعیت‌های موجود منتقل می‌شوند؛ تاریخ پایان، مصرف‌کننده یا سقف تجاری حدس زده نمی‌شود.
            migrationBuilder.Sql("""
                EXEC(N'UPDATE [commerce].[Subscriptions] SET [OwnerType]=2,[OwnerId]=[OrganizationId],[InitialPlanVersionId]=[PlanVersionId];');
                EXEC(N'UPDATE l SET [OwnerType]=2,[OwnerId]=l.[OrganizationId],[Status]=2,[PlanVersionId]=s.[PlanVersionId],
                    [ContextType]=3,[ContextId]=l.[OrganizationId],[PeriodStartsAtUtc]=s.[StartsAtUtc],[PeriodEndsAtUtc]=s.[EndsAtUtc],
                    [ReasonCode]=N''legacy_consumed'' FROM [commerce].[UsageLedger] l JOIN [commerce].[Subscriptions] s ON s.[Id]=l.[SubscriptionId];');
                EXEC(N'UPDATE [commerce].[Plans] SET [Audience]=2;');
                EXEC(N'UPDATE [commerce].[Orders] SET [Purpose]=1;');
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Subscriptions_Owner_Id",
                schema: "commerce",
                table: "Subscriptions",
                columns: new[] { "OwnerType", "OwnerId", "Id" });

            migrationBuilder.CreateTable(
                name: "PersonalClasses",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalClasses_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionAmendments",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextPlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemainingTicks = table.Column<long>(type: "bigint", nullable: false),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    ChargedToman = table.Column<decimal>(type: "decimal(18,0)", precision: 18, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionAmendments", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionAmendments_Proration", "[DurationTicks]>0 AND [RemainingTicks]>=0 AND [RemainingTicks]<=[DurationTicks] AND [ChargedToman]>=0");
                    table.ForeignKey(
                        name: "FK_SubscriptionAmendments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "commerce",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionAmendments_PlanVersions_NextPlanVersionId",
                        column: x => x.NextPlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionAmendments_PlanVersions_PreviousPlanVersionId",
                        column: x => x.PreviousPlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionAmendments_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "commerce",
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsageReservations",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<int>(type: "int", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConsumerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContextType = table.Column<int>(type: "int", nullable: false),
                    ContextId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuotaKey = table.Column<int>(type: "int", nullable: false),
                    PeriodStartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    IsTrial = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizationReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageReservations", x => x.Id);
                    table.UniqueConstraint("AK_UsageReservations_OwnerType_OwnerId_ConsumerUserId_Id", x => new { x.OwnerType, x.OwnerId, x.ConsumerUserId, x.Id });
                    table.CheckConstraint("CK_UsageReservations_Finalization", "([Status] IN (1,4) AND [FinalizedAtUtc] IS NULL) OR ([Status] IN (2,3) AND [FinalizedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_UsageReservations_Trial", "([IsTrial]=1 AND [SubscriptionId] IS NULL AND [PlanVersionId] IS NULL AND [OwnerType]=1 AND [OwnerId]=[ConsumerUserId] AND [QuotaKey]=5 AND [Amount]=1) OR ([IsTrial]=0 AND [SubscriptionId] IS NOT NULL AND [PlanVersionId] IS NOT NULL)");
                    table.CheckConstraint("CK_UsageReservations_Values", "[Amount]>0 AND [Status] BETWEEN 1 AND 4 AND [OwnerType] IN (1,2) AND [ContextType] BETWEEN 1 AND 3 AND [QuotaKey] BETWEEN 1 AND 7 AND [PeriodEndsAtUtc]>[PeriodStartsAtUtc]");
                    table.ForeignKey(
                        name: "FK_UsageReservations_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsageReservations_Subscriptions_OwnerType_OwnerId_SubscriptionId",
                        columns: x => new { x.OwnerType, x.OwnerId, x.SubscriptionId },
                        principalSchema: "commerce",
                        principalTable: "Subscriptions",
                        principalColumns: new[] { "OwnerType", "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsageReservations_Users_ConsumerUserId",
                        column: x => x.ConsumerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalClassInvitations",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinationUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalClassInvitations", x => x.Id);
                    table.UniqueConstraint("AK_PersonalClassInvitations_PersonalClassId_DestinationUserId_Id", x => new { x.PersonalClassId, x.DestinationUserId, x.Id });
                    table.CheckConstraint("CK_PersonalClassInvitations_Expiry", "[ExpiresAtUtc]>[CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_PersonalClassInvitations_PersonalClasses_PersonalClassId",
                        column: x => x.PersonalClassId,
                        principalSchema: "academic",
                        principalTable: "PersonalClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassInvitations_Users_DestinationUserId",
                        column: x => x.DestinationUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalClassParticipants",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalClassParticipants", x => x.Id);
                    table.CheckConstraint("CK_PersonalClassParticipants_End", "[EndedAtUtc] IS NULL OR [EndedAtUtc]>=[JoinedAtUtc]");
                    table.ForeignKey(
                        name: "FK_PersonalClassParticipants_PersonalClassInvitations_PersonalClassId_StudentUserId_InvitationId",
                        columns: x => new { x.PersonalClassId, x.StudentUserId, x.InvitationId },
                        principalSchema: "academic",
                        principalTable: "PersonalClassInvitations",
                        principalColumns: new[] { "PersonalClassId", "DestinationUserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassParticipants_Users_StudentUserId",
                        column: x => x.StudentUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_ConsumerUserId",
                schema: "commerce",
                table: "UsageLedger",
                column: "ConsumerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_OwnerType_OwnerId_ConsumerUserId_ReservationId",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "OwnerType", "OwnerId", "ConsumerUserId", "ReservationId" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_OwnerType_OwnerId_SubscriptionId",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "OwnerType", "OwnerId", "SubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_PlanVersionId",
                schema: "commerce",
                table: "UsageLedger",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageLedger_ReservationId_Status",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "ReservationId", "Status" },
                unique: true,
                filter: "[ReservationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_UsageLedger_Subscription_Quota_SourceOperation",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "SubscriptionId", "QuotaKey", "SourceOperationId" },
                unique: true,
                filter: "[Status]=2 AND [SubscriptionId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_Owner",
                schema: "commerce",
                table: "UsageLedger",
                sql: "([OwnerType]=1 AND [OrganizationId] IS NULL) OR ([OwnerType]=2 AND [OrganizationId]=[OwnerId] AND [OrganizationId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_QuotaKey",
                schema: "commerce",
                table: "UsageLedger",
                sql: "[QuotaKey] BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_Reservation",
                schema: "commerce",
                table: "UsageLedger",
                sql: "[ReservationId] IS NULL OR ([ConsumerUserId] IS NOT NULL AND [ContextType] IS NOT NULL AND [ContextId] IS NOT NULL AND [PeriodStartsAtUtc] IS NOT NULL AND [PeriodEndsAtUtc]>[PeriodStartsAtUtc])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_Status",
                schema: "commerce",
                table: "UsageLedger",
                sql: "[Status] BETWEEN 1 AND 4");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_InitialPlanVersionId",
                schema: "commerce",
                table: "Subscriptions",
                column: "InitialPlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Owner_Period",
                schema: "commerce",
                table: "Subscriptions",
                columns: new[] { "OwnerType", "OwnerId", "StartsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_OwnerUserId",
                schema: "commerce",
                table: "Subscriptions",
                column: "OwnerUserId",
                unique: true,
                filter: "[Status]=2 AND [OwnerUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_ScheduledPlanVersionId",
                schema: "commerce",
                table: "Subscriptions",
                column: "ScheduledPlanVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_Organization_Active",
                schema: "commerce",
                table: "Subscriptions",
                column: "OrganizationId",
                unique: true,
                filter: "[Status] = 2 AND [OrganizationId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_ManagedPeriod",
                schema: "commerce",
                table: "Subscriptions",
                sql: "[CommercialControlEnabled]=0 OR ([BillingAnchorUtc] IS NOT NULL AND [EndsAtUtc] IS NOT NULL AND [BillingPeriodIndex]>=0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_Owner",
                schema: "commerce",
                table: "Subscriptions",
                sql: "([OwnerType]=1 AND [OwnerUserId]=[OwnerId] AND [OwnerUserId] IS NOT NULL AND [OrganizationId] IS NULL) OR ([OwnerType]=2 AND [OrganizationId]=[OwnerId] AND [OrganizationId] IS NOT NULL AND [OwnerUserId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_Status",
                schema: "commerce",
                table: "Subscriptions",
                sql: "[Status] BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanVersions_Calendar",
                schema: "commerce",
                table: "PlanVersions",
                sql: "[Calendar] IS NULL OR [Calendar] IN (1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanVersions_Warnings",
                schema: "commerce",
                table: "PlanVersions",
                sql: "([WarningPercent] IS NULL OR [WarningPercent] BETWEEN 1 AND 99) AND ([CriticalWarningPercent] IS NULL OR [CriticalWarningPercent] BETWEEN 1 AND 99) AND ([WarningPercent] IS NULL OR [CriticalWarningPercent] IS NULL OR [WarningPercent]<[CriticalWarningPercent])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanVersionPrices_Amount",
                schema: "commerce",
                table: "PlanVersionPrices",
                sql: "[Amount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Plans_Audience",
                schema: "commerce",
                table: "Plans",
                sql: "[Audience] IN (1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanQuotaDefinitions_Key",
                schema: "commerce",
                table: "PlanQuotaDefinitions",
                sql: "[Key] BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanEntitlements_Key",
                schema: "commerce",
                table: "PlanEntitlements",
                sql: "[Key] BETWEEN 1 AND 9");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ExpectedPreviousPlanVersionId",
                schema: "commerce",
                table: "Orders",
                column: "ExpectedPreviousPlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TargetSubscriptionId",
                schema: "commerce",
                table: "Orders",
                column: "TargetSubscriptionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Amount",
                schema: "commerce",
                table: "Orders",
                sql: "[Amount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_CommercialPurpose",
                schema: "commerce",
                table: "Orders",
                sql: "[Purpose] BETWEEN 1 AND 3 AND ([HasCommercialTerms]=0 OR ([BillingAnchorUtc] IS NOT NULL AND [TermsExpireAtUtc]>[CreatedAtUtc]))");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClasses_HostUserId_ClientOperationId",
                schema: "academic",
                table: "PersonalClasses",
                columns: new[] { "HostUserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassInvitations_DestinationUserId",
                schema: "academic",
                table: "PersonalClassInvitations",
                column: "DestinationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassInvitations_PersonalClassId_ClientOperationId",
                schema: "academic",
                table: "PersonalClassInvitations",
                columns: new[] { "PersonalClassId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassParticipants_InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants",
                column: "InvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassParticipants_PersonalClassId_StudentUserId",
                schema: "academic",
                table: "PersonalClassParticipants",
                columns: new[] { "PersonalClassId", "StudentUserId" },
                unique: true,
                filter: "[EndedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassParticipants_PersonalClassId_StudentUserId_InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants",
                columns: new[] { "PersonalClassId", "StudentUserId", "InvitationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassParticipants_StudentUserId",
                schema: "academic",
                table: "PersonalClassParticipants",
                column: "StudentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAmendments_NextPlanVersionId",
                schema: "commerce",
                table: "SubscriptionAmendments",
                column: "NextPlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAmendments_OrderId",
                schema: "commerce",
                table: "SubscriptionAmendments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAmendments_PreviousPlanVersionId",
                schema: "commerce",
                table: "SubscriptionAmendments",
                column: "PreviousPlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionAmendments_SubscriptionId",
                schema: "commerce",
                table: "SubscriptionAmendments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_UsageReservations_ConsumerUserId_OperationId",
                schema: "commerce",
                table: "UsageReservations",
                columns: new[] { "ConsumerUserId", "OperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageReservations_OwnerType_OwnerId_SubscriptionId_QuotaKey_PeriodStartsAtUtc_Status",
                schema: "commerce",
                table: "UsageReservations",
                columns: new[] { "OwnerType", "OwnerId", "SubscriptionId", "QuotaKey", "PeriodStartsAtUtc", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageReservations_PlanVersionId",
                schema: "commerce",
                table: "UsageReservations",
                column: "PlanVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_PlanVersions_ExpectedPreviousPlanVersionId",
                schema: "commerce",
                table: "Orders",
                column: "ExpectedPreviousPlanVersionId",
                principalSchema: "commerce",
                principalTable: "PlanVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Subscriptions_TargetSubscriptionId",
                schema: "commerce",
                table: "Orders",
                column: "TargetSubscriptionId",
                principalSchema: "commerce",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_PlanVersions_InitialPlanVersionId",
                schema: "commerce",
                table: "Subscriptions",
                column: "InitialPlanVersionId",
                principalSchema: "commerce",
                principalTable: "PlanVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_PlanVersions_ScheduledPlanVersionId",
                schema: "commerce",
                table: "Subscriptions",
                column: "ScheduledPlanVersionId",
                principalSchema: "commerce",
                principalTable: "PlanVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_OwnerUserId",
                schema: "commerce",
                table: "Subscriptions",
                column: "OwnerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsageLedger_PlanVersions_PlanVersionId",
                schema: "commerce",
                table: "UsageLedger",
                column: "PlanVersionId",
                principalSchema: "commerce",
                principalTable: "PlanVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsageLedger_Subscriptions_OwnerType_OwnerId_SubscriptionId",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "OwnerType", "OwnerId", "SubscriptionId" },
                principalSchema: "commerce",
                principalTable: "Subscriptions",
                principalColumns: new[] { "OwnerType", "OwnerId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsageLedger_UsageReservations_OwnerType_OwnerId_ConsumerUserId_ReservationId",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "OwnerType", "OwnerId", "ConsumerUserId", "ReservationId" },
                principalSchema: "commerce",
                principalTable: "UsageReservations",
                principalColumns: new[] { "OwnerType", "OwnerId", "ConsumerUserId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UsageLedger_Users_ConsumerUserId",
                schema: "commerce",
                table: "UsageLedger",
                column: "ConsumerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // برگشت به مدل سازمانی قدیمی نباید دادهٔ واقعی قابلیت تازه را بی‌صدا حذف کند.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [commerce].[Subscriptions] WHERE [OwnerType]=1 OR [CommercialControlEnabled]=1)
                   OR EXISTS (SELECT 1 FROM [commerce].[UsageReservations])
                   OR EXISTS (SELECT 1 FROM [commerce].[SubscriptionAmendments])
                   OR EXISTS (SELECT 1 FROM [academic].[PersonalClasses])
                   OR EXISTS (SELECT 1 FROM [commerce].[PlanVersions] WHERE [Calendar] IS NOT NULL)
                   THROW 51000, N'Rollback requires an explicit data-preservation plan for commercial contracts.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_PlanVersions_ExpectedPreviousPlanVersionId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Subscriptions_TargetSubscriptionId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_PlanVersions_InitialPlanVersionId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_PlanVersions_ScheduledPlanVersionId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Users_OwnerUserId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_UsageLedger_PlanVersions_PlanVersionId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_UsageLedger_Subscriptions_OwnerType_OwnerId_SubscriptionId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_UsageLedger_UsageReservations_OwnerType_OwnerId_ConsumerUserId_ReservationId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropForeignKey(
                name: "FK_UsageLedger_Users_ConsumerUserId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropTable(
                name: "PersonalClassParticipants",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "SubscriptionAmendments",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "UsageReservations",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PersonalClassInvitations",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "PersonalClasses",
                schema: "academic");

            migrationBuilder.DropIndex(
                name: "IX_UsageLedger_ConsumerUserId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropIndex(
                name: "IX_UsageLedger_OwnerType_OwnerId_ConsumerUserId_ReservationId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropIndex(
                name: "IX_UsageLedger_OwnerType_OwnerId_SubscriptionId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropIndex(
                name: "IX_UsageLedger_PlanVersionId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropIndex(
                name: "IX_UsageLedger_ReservationId_Status",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropIndex(
                name: "UX_UsageLedger_Subscription_Quota_SourceOperation",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_Owner",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_QuotaKey",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_Reservation",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UsageLedger_Status",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Subscriptions_Owner_Id",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_InitialPlanVersionId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_Owner_Period",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_OwnerUserId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_ScheduledPlanVersionId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_Organization_Active",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_ManagedPeriod",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_Owner",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subscriptions_Status",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanVersions_Calendar",
                schema: "commerce",
                table: "PlanVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanVersions_Warnings",
                schema: "commerce",
                table: "PlanVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanVersionPrices_Amount",
                schema: "commerce",
                table: "PlanVersionPrices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Plans_Audience",
                schema: "commerce",
                table: "Plans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanQuotaDefinitions_Key",
                schema: "commerce",
                table: "PlanQuotaDefinitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlanEntitlements_Key",
                schema: "commerce",
                table: "PlanEntitlements");

            migrationBuilder.DropIndex(
                name: "IX_Orders_ExpectedPreviousPlanVersionId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TargetSubscriptionId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Amount",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_CommercialPurpose",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConsumerUserId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "ContextId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "ContextType",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "PeriodEndsAtUtc",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "PeriodStartsAtUtc",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "PlanVersionId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "ReservationId",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "commerce",
                table: "UsageLedger");

            migrationBuilder.DropColumn(
                name: "BillingAnchorUtc",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "BillingPeriodIndex",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CommercialControlEnabled",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "InitialPlanVersionId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ScheduledPlanVersionId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Calendar",
                schema: "commerce",
                table: "PlanVersions");

            migrationBuilder.DropColumn(
                name: "CriticalWarningPercent",
                schema: "commerce",
                table: "PlanVersions");

            migrationBuilder.DropColumn(
                name: "ExplicitlyFree",
                schema: "commerce",
                table: "PlanVersions");

            migrationBuilder.DropColumn(
                name: "SaleTermsReference",
                schema: "commerce",
                table: "PlanVersions");

            migrationBuilder.DropColumn(
                name: "WarningPercent",
                schema: "commerce",
                table: "PlanVersions");

            migrationBuilder.DropColumn(
                name: "Audience",
                schema: "commerce",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "AvailabilityEffectiveAtUtc",
                schema: "commerce",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "CatalogVisible",
                schema: "commerce",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "NewPurchaseAvailable",
                schema: "commerce",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "RenewalAvailable",
                schema: "commerce",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "BillingAnchorUtc",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillingPeriodIndex",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ExpectedPreviousPlanVersionId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HasCommercialTerms",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Purpose",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TargetSubscriptionId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TermsExpireAtUtc",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubscriptionId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "commerce",
                table: "UsageLedger",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Subscriptions_Organization_Id",
                schema: "commerce",
                table: "Subscriptions",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_UsageLedger_Subscription_Quota_SourceOperation",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "SubscriptionId", "QuotaKey", "SourceOperationId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UsageLedger_QuotaKey",
                schema: "commerce",
                table: "UsageLedger",
                sql: "[QuotaKey] BETWEEN 1 AND 4");

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_Organization_Active",
                schema: "commerce",
                table: "Subscriptions",
                column: "OrganizationId",
                unique: true,
                filter: "[Status] = 2");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subscriptions_Status",
                schema: "commerce",
                table: "Subscriptions",
                sql: "[Status] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanVersionPrices_Amount",
                schema: "commerce",
                table: "PlanVersionPrices",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanQuotaDefinitions_Key",
                schema: "commerce",
                table: "PlanQuotaDefinitions",
                sql: "[Key] BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlanEntitlements_Key",
                schema: "commerce",
                table: "PlanEntitlements",
                sql: "[Key] BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Amount",
                schema: "commerce",
                table: "Orders",
                sql: "[Amount] > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_UsageLedger_Subscriptions_OrganizationId_SubscriptionId",
                schema: "commerce",
                table: "UsageLedger",
                columns: new[] { "OrganizationId", "SubscriptionId" },
                principalSchema: "commerce",
                principalTable: "Subscriptions",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
