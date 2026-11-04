using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenOrganizationClassCreationIntent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientOperationId",
                schema: "academic",
                table: "Classes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "academic",
                table: "Classes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreationRequestHash",
                schema: "academic",
                table: "Classes",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classes_CreatedByUserId",
                schema: "academic",
                table: "Classes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Classes_CreationIntent",
                schema: "academic",
                table: "Classes",
                columns: new[] { "OrganizationId", "CreatedByUserId", "ClientOperationId" },
                unique: true,
                filter: "[ClientOperationId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Classes_CreationIntent",
                schema: "academic",
                table: "Classes",
                sql: "([CreatedByUserId] IS NULL AND [ClientOperationId] IS NULL AND [CreationRequestHash] IS NULL) OR ([CreatedByUserId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [CreationRequestHash] IS NOT NULL AND LEN([CreationRequestHash]) = 64)");

            migrationBuilder.AddForeignKey(
                name: "FK_Classes_Users_CreatedByUserId",
                schema: "academic",
                table: "Classes",
                column: "CreatedByUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // پس از استفادهٔ واقعی، حذف رسید قصد می‌تواند در بازانتشار باعث ایجاد کلاس تکراری شود.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [academic].[Classes] WHERE [ClientOperationId] IS NOT NULL) THROW 51000, N'برگشت Migration پس از ثبت قصد ایجاد کلاس نیازمند طرح صریح حفظ رسیدها است.', 1;");
            migrationBuilder.DropForeignKey(
                name: "FK_Classes_Users_CreatedByUserId",
                schema: "academic",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_Classes_CreatedByUserId",
                schema: "academic",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "UX_Classes_CreationIntent",
                schema: "academic",
                table: "Classes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Classes_CreationIntent",
                schema: "academic",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "ClientOperationId",
                schema: "academic",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "academic",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "CreationRequestHash",
                schema: "academic",
                table: "Classes");
        }
    }
}
