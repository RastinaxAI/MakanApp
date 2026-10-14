using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalPersonGuardianOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invitations_OrganizationId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.AddColumn<Guid>(
                name: "CreationOperationId",
                schema: "organization",
                table: "OrganizationPersons",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreationRequestHash",
                schema: "organization",
                table: "OrganizationPersons",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientOperationId",
                schema: "organization",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationPersonId",
                schema: "organization",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_OrganizationPersons_CreationOperation",
                schema: "organization",
                table: "OrganizationPersons",
                columns: new[] { "OrganizationId", "CreationOperationId" },
                unique: true,
                filter: "[CreationOperationId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrganizationPersons_CreationRequest",
                schema: "organization",
                table: "OrganizationPersons",
                sql: "([CreationOperationId] IS NULL AND [CreationRequestHash] IS NULL) OR ([CreationOperationId] IS NOT NULL AND [CreationRequestHash] IS NOT NULL AND LEN([CreationRequestHash]) = 64)");

            migrationBuilder.CreateIndex(
                name: "UX_Invitations_LinkOperation",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "OrganizationId", "ClientOperationId" },
                unique: true,
                filter: "[ClientOperationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Invitations_Pending_PersonLink",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "OrganizationId", "OrganizationPersonId" },
                unique: true,
                filter: "[OrganizationPersonId] IS NOT NULL AND [Status] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invitations_PersonLink",
                schema: "organization",
                table: "Invitations",
                sql: "([OrganizationPersonId] IS NULL AND [ClientOperationId] IS NULL) OR ([OrganizationPersonId] IS NOT NULL AND [ClientOperationId] IS NOT NULL AND [Role] = 1)");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_OrganizationPersons_OrganizationId_OrganizationPersonId",
                schema: "organization",
                table: "Invitations",
                columns: new[] { "OrganizationId", "OrganizationPersonId" },
                principalSchema: "organization",
                principalTable: "OrganizationPersons",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_OrganizationPersons_OrganizationId_OrganizationPersonId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "UX_OrganizationPersons_CreationOperation",
                schema: "organization",
                table: "OrganizationPersons");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrganizationPersons_CreationRequest",
                schema: "organization",
                table: "OrganizationPersons");

            migrationBuilder.DropIndex(
                name: "UX_Invitations_LinkOperation",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "UX_Invitations_Pending_PersonLink",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invitations_PersonLink",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "CreationOperationId",
                schema: "organization",
                table: "OrganizationPersons");

            migrationBuilder.DropColumn(
                name: "CreationRequestHash",
                schema: "organization",
                table: "OrganizationPersons");

            migrationBuilder.DropColumn(
                name: "ClientOperationId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "OrganizationPersonId",
                schema: "organization",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_OrganizationId",
                schema: "organization",
                table: "Invitations",
                column: "OrganizationId");
        }
    }
}
