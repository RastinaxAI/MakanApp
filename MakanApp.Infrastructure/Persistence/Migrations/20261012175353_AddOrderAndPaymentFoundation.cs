using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAndPaymentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceOrderId",
                schema: "commerce",
                table: "Subscriptions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaserUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestPayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Orders_Currency", "[Currency] = 'IRR'");
                    table.CheckConstraint("CK_Orders_Lifecycle", "([Status] = 1 AND [PaidAtUtc] IS NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = 2 AND [PaidAtUtc] IS NOT NULL AND [CancelledAtUtc] IS NULL) OR ([Status] = 3 AND [PaidAtUtc] IS NULL AND [CancelledAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_Orders_RequestPayloadHash", "DATALENGTH([RequestPayloadHash]) = 32");
                    table.CheckConstraint("CK_Orders_Status", "[Status] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Orders_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Users_PurchaserUserId",
                        column: x => x.PurchaserUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanVersionPrices",
                schema: "commerce",
                columns: table => new
                {
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanVersionPrices", x => x.PlanVersionId);
                    table.CheckConstraint("CK_PlanVersionPrices_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PlanVersionPrices_Currency", "[Currency] = 'IRR'");
                    table.ForeignKey(
                        name: "FK_PlanVersionPrices_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestPayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaymentUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    InitiatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InitiationClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiationClaimedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                    table.CheckConstraint("CK_PaymentAttempts_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_PaymentAttempts_Claim", "([Status] = 2 AND [InitiationClaimToken] IS NOT NULL AND [InitiationClaimedUntilUtc] IS NOT NULL) OR ([Status] <> 2 AND [InitiationClaimToken] IS NULL AND [InitiationClaimedUntilUtc] IS NULL)");
                    table.CheckConstraint("CK_PaymentAttempts_Currency", "[Currency] = 'IRR'");
                    table.CheckConstraint("CK_PaymentAttempts_RequestPayloadHash", "DATALENGTH([RequestPayloadHash]) = 32");
                    table.CheckConstraint("CK_PaymentAttempts_Status", "[Status] BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_PaymentAttempts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "commerce",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_Payments_Currency", "[Currency] = 'IRR'");
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "commerce",
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_PaymentAttempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalSchema: "commerce",
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Subscriptions_SourceOrder",
                schema: "commerce",
                table: "Subscriptions",
                column: "SourceOrderId",
                unique: true,
                filter: "[SourceOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Organization_Status_CreatedAt",
                schema: "commerce",
                table: "Orders",
                columns: new[] { "OrganizationId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PlanVersionId",
                schema: "commerce",
                table: "Orders",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Purchaser_CreatedAt",
                schema: "commerce",
                table: "Orders",
                columns: new[] { "PurchaserUserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_Purchaser_ClientOperation",
                schema: "commerce",
                table: "Orders",
                columns: new[] { "PurchaserUserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Order_Status",
                schema: "commerce",
                table: "PaymentAttempts",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentAttempts_Order_ClientOperation",
                schema: "commerce",
                table: "PaymentAttempts",
                columns: new[] { "OrderId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentAttempts_Provider_Reference",
                schema: "commerce",
                table: "PaymentAttempts",
                columns: new[] { "Provider", "ProviderReference" },
                unique: true,
                filter: "[ProviderReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_Attempt",
                schema: "commerce",
                table: "Payments",
                column: "PaymentAttemptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Payments_Order",
                schema: "commerce",
                table: "Payments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Payments_Provider_Reference",
                schema: "commerce",
                table: "Payments",
                columns: new[] { "Provider", "ProviderReference" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Orders_SourceOrderId",
                schema: "commerce",
                table: "Subscriptions",
                column: "SourceOrderId",
                principalSchema: "commerce",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Orders_SourceOrderId",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "Payments",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PlanVersionPrices",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "PaymentAttempts",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "commerce");

            migrationBuilder.DropIndex(
                name: "UX_Subscriptions_SourceOrder",
                schema: "commerce",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "SourceOrderId",
                schema: "commerce",
                table: "Subscriptions");
        }
    }
}
