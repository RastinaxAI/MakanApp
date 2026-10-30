using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSourceSelectionPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSourceSelectionOperations",
                schema: "knowledge",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSourceSelectionOperations", x => new { x.UserId, x.OperationId });
                    table.CheckConstraint("CK_AiSourceSelectionOperations_Hash", "DATALENGTH([PayloadHash])=32");
                    table.ForeignKey(
                        name: "FK_AiSourceSelectionOperations_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiSourceSelectionPins",
                schema: "knowledge",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSourceSelectionPins", x => new { x.UserId, x.OperationId, x.SourceId });
                    table.CheckConstraint("CK_AiSourceSelectionPins_Number", "[Number]>0");
                    table.ForeignKey(
                        name: "FK_AiSourceSelectionPins_AiSourceSelectionOperations_UserId_OperationId",
                        columns: x => new { x.UserId, x.OperationId },
                        principalSchema: "knowledge",
                        principalTable: "AiSourceSelectionOperations",
                        principalColumns: new[] { "UserId", "OperationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiSourceSelectionPins_EducationalSourceVersions_SourceId_Number",
                        columns: x => new { x.SourceId, x.Number },
                        principalSchema: "knowledge",
                        principalTable: "EducationalSourceVersions",
                        principalColumns: new[] { "SourceId", "Number" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiSourceSelectionPins_SourceId_Number",
                schema: "knowledge",
                table: "AiSourceSelectionPins",
                columns: new[] { "SourceId", "Number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [knowledge].[AiSourceSelectionOperations]) THROW 51133, N'Used AI selection history cannot be dropped', 1;");
            migrationBuilder.DropTable(
                name: "AiSourceSelectionPins",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "AiSourceSelectionOperations",
                schema: "knowledge");
        }
    }
}
