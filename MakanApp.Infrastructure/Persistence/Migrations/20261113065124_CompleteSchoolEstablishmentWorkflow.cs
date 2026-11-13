using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSchoolEstablishmentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolApplications_Status",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolApplications_SubmissionPolicy",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolApplicationId",
                schema: "platform",
                table: "ProvisioningRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceOrderId",
                schema: "platform",
                table: "ProvisioningRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolApplicationId",
                schema: "commerce",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SchoolApplicationHistory",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolApplicationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchoolApplicationHistory_SchoolApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "organization",
                        principalTable: "SchoolApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolApplicationHistory_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolEstablishmentOfferings",
                schema: "commerce",
                columns: table => new
                {
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolEstablishmentOfferings", x => x.Kind);
                    table.CheckConstraint("CK_SchoolOfferings_KindVersion", "[Kind] IN (1,2) AND [Version]>0");
                    table.ForeignKey(
                        name: "FK_SchoolEstablishmentOfferings_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SchoolOfferingOperations",
                schema: "commerce",
                columns: table => new
                {
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    PlanVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferingVersion = table.Column<long>(type: "bigint", nullable: false),
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolOfferingOperations", x => new { x.ActorUserId, x.ClientOperationId });
                    table.ForeignKey(
                        name: "FK_SchoolOfferingOperations_PlanVersions_PlanVersionId",
                        column: x => x.PlanVersionId,
                        principalSchema: "commerce",
                        principalTable: "PlanVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolOfferingOperations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolApplications_Capacities",
                schema: "organization",
                table: "SchoolApplications",
                sql: "([StudentCapacity] IS NULL OR [StudentCapacity]>=0) AND ([TeacherCapacity] IS NULL OR [TeacherCapacity]>=0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolApplications_Status",
                schema: "organization",
                table: "SchoolApplications",
                sql: "[Status] IN (1,2,3,4,5,6,7)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolApplications_SubmissionPolicy",
                schema: "organization",
                table: "SchoolApplications",
                sql: "[Status] IN (1,7) OR [SubmittedPolicyVersion] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_SchoolApplicationId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "SchoolApplicationId",
                unique: true,
                filter: "[SchoolApplicationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProvisioningRequests_SourceOrderId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "SourceOrderId",
                unique: true,
                filter: "[SourceOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SchoolApplicationId",
                schema: "commerce",
                table: "Orders",
                column: "SchoolApplicationId",
                unique: true,
                filter: "[SchoolApplicationId] IS NOT NULL AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationHistory_ActorUserId",
                schema: "organization",
                table: "SchoolApplicationHistory",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolApplicationHistory_ApplicationId_Version",
                schema: "organization",
                table: "SchoolApplicationHistory",
                columns: new[] { "ApplicationId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolEstablishmentOfferings_PlanVersionId",
                schema: "commerce",
                table: "SchoolEstablishmentOfferings",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolOfferingOperations_PlanVersionId",
                schema: "commerce",
                table: "SchoolOfferingOperations",
                column: "PlanVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_SchoolApplications_SchoolApplicationId",
                schema: "commerce",
                table: "Orders",
                column: "SchoolApplicationId",
                principalSchema: "organization",
                principalTable: "SchoolApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProvisioningRequests_Orders_SourceOrderId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "SourceOrderId",
                principalSchema: "commerce",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProvisioningRequests_SchoolApplications_SchoolApplicationId",
                schema: "platform",
                table: "ProvisioningRequests",
                column: "SchoolApplicationId",
                principalSchema: "organization",
                principalTable: "SchoolApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_SchoolApplications_SchoolApplicationId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProvisioningRequests_Orders_SourceOrderId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_ProvisioningRequests_SchoolApplications_SchoolApplicationId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropTable(
                name: "SchoolApplicationHistory",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "SchoolEstablishmentOfferings",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "SchoolOfferingOperations",
                schema: "commerce");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolApplications_Capacities",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolApplications_Status",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchoolApplications_SubmissionPolicy",
                schema: "organization",
                table: "SchoolApplications");

            migrationBuilder.DropIndex(
                name: "IX_ProvisioningRequests_SchoolApplicationId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropIndex(
                name: "IX_ProvisioningRequests_SourceOrderId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SchoolApplicationId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SchoolApplicationId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropColumn(
                name: "SourceOrderId",
                schema: "platform",
                table: "ProvisioningRequests");

            migrationBuilder.DropColumn(
                name: "SchoolApplicationId",
                schema: "commerce",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolApplications_Status",
                schema: "organization",
                table: "SchoolApplications",
                sql: "[Status] IN (1,2,3,4,5,6)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchoolApplications_SubmissionPolicy",
                schema: "organization",
                table: "SchoolApplications",
                sql: "[Status]=1 OR [SubmittedPolicyVersion] IS NOT NULL");
        }
    }
}
