using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilePhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfilePhotoFileAssetId",
                schema: "identity",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProfilePhotoFileAssetId",
                schema: "identity",
                table: "Users",
                column: "ProfilePhotoFileAssetId",
                unique: true,
                filter: "[ProfilePhotoFileAssetId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_FileAssets_ProfilePhotoFileAssetId",
                schema: "identity",
                table: "Users",
                column: "ProfilePhotoFileAssetId",
                principalSchema: "storage",
                principalTable: "FileAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_FileAssets_ProfilePhotoFileAssetId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ProfilePhotoFileAssetId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfilePhotoFileAssetId",
                schema: "identity",
                table: "Users");
        }
    }
}
