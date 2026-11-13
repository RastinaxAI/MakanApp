using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeSchoolManagerAndPaidProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProvisioningRequests_OrganizationId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.AddColumn<Guid>(
                name: "FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InitialManagerConfirmedAtUtc",
                schema: "organization",
                table: "SchoolApplications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitialManagerConfirmedByUserId",
                schema: "organization",
                table: "SchoolApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ApplicationVersion",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePaymentId",
                schema: "platform",
                table: "ProvisioningRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplications_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplications",
                column: "FinalInitialManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplications_InitialManagerConfirmedByUserId",
                schema: "organization",
                table: "SchoolApplications",
                column: "InitialManagerConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationHistory_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory",
                column: "FinalInitialManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationHistory_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory",
                column: "ProposedInitialManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationDecisions_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                column: "FinalInitialManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationDecisions_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                column: "ProposedInitialManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_OrganizationId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "OrganizationId",
                unique: true,
                filter: "[SchoolApplicationId] IS NOT NULL AND [OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_SourcePaymentId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "SourcePaymentId",
                unique: true,
                filter: "[SourcePaymentId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ProvisioningRequests_Payments_SourcePaymentId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "SourcePaymentId",
                principalSchema: "commerce",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolApplicationDecisions_Users_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                column: "FinalInitialManagerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolApplicationDecisions_Users_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions",
                column: "ProposedInitialManagerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolApplicationHistory_Users_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory",
                column: "FinalInitialManagerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolApplicationHistory_Users_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory",
                column: "ProposedInitialManagerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolApplications_Users_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplications",
                column: "FinalInitialManagerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolApplications_Users_InitialManagerConfirmedByUserId",
                schema: "organization",
                table: "SchoolApplications",
                column: "InitialManagerConfirmedByUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProvisioningRequests_Payments_SourcePaymentId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolApplicationDecisions_Users_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolApplicationDecisions_Users_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolApplicationHistory_Users_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolApplicationHistory_Users_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolApplications_Users_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolApplications_Users_InitialManagerConfirmedByUserId",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropIndex(
                name: "IX_SchoolApplications_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropIndex(
                name: "IX_SchoolApplications_InitialManagerConfirmedByUserId",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropIndex(
                name: "IX_SchoolApplicationHistory_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory");

            migrationBuilder.DropIndex(
                name: "IX_SchoolApplicationHistory_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory");

            migrationBuilder.DropIndex(
                name: "IX_SchoolApplicationDecisions_FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions");

            migrationBuilder.DropIndex(
                name: "IX_SchoolApplicationDecisions_ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions");

            migrationBuilder.DropIndex(
                name: "IX_ProvisioningRequests_OrganizationId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropIndex(
                name: "IX_ProvisioningRequests_SourcePaymentId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropColumn(
                name: "FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropColumn(
                name: "InitialManagerConfirmedAtUtc",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropColumn(
                name: "InitialManagerConfirmedByUserId",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropColumn(
                name: "FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory");

            migrationBuilder.DropColumn(
                name: "ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationHistory");

            migrationBuilder.DropColumn(
                name: "ApplicationVersion",
                schema: "organization",
                table: "SchoolApplicationDecisions");

            migrationBuilder.DropColumn(
                name: "FinalInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions");

            migrationBuilder.DropColumn(
                name: "ProposedInitialManagerUserId",
                schema: "organization",
                table: "SchoolApplicationDecisions");

            migrationBuilder.DropColumn(
                name: "SourcePaymentId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_OrganizationId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "OrganizationId");
        }
    }
}
