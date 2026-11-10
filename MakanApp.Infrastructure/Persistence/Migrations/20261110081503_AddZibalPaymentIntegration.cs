using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZibalPaymentIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderPaidAtUtc",
                schema: "commerce",
                table: "Payments",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementReference",
                schema: "commerce",
                table: "Payments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CallbackReceivedAtUtc",
                schema: "commerce",
                table: "PaymentAttempts",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderPaidAtUtc",
                schema: "commerce",
                table: "PaymentAttempts",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SettlementReference",
                schema: "commerce",
                table: "PaymentAttempts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductTitleSnapshot",
                schema: "commerce",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentAttemptEvents",
                schema: "commerce",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ProviderResult = table.Column<int>(type: "int", nullable: true),
                    ProviderStatus = table.Column<int>(type: "int", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttemptEvents", x => x.Id);
                    table.CheckConstraint("CK_PaymentAttemptEvents_Type", "[Type] BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "FK_PaymentAttemptEvents_PaymentAttempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalSchema: "commerce",
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttemptEvents_PaymentAttemptId_OccurredAtUtc_Id",
                schema: "commerce",
                table: "PaymentAttemptEvents",
                columns: new[] { "PaymentAttemptId", "OccurredAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAttemptEvents",
                schema: "commerce");

            migrationBuilder.DropColumn(
                name: "ProviderPaidAtUtc",
                schema: "commerce",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "SettlementReference",
                schema: "commerce",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CallbackReceivedAtUtc",
                schema: "commerce",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "ProviderPaidAtUtc",
                schema: "commerce",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "SettlementReference",
                schema: "commerce",
                table: "PaymentAttempts");

            migrationBuilder.DropColumn(
                name: "ProductTitleSnapshot",
                schema: "commerce",
                table: "Orders");
        }
    }
}
