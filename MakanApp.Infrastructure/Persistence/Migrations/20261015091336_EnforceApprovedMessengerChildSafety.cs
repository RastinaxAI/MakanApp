using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceApprovedMessengerChildSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions");

            migrationBuilder.AddColumn<int>(
                name: "CommunicationAgeVerificationStatus",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "MessagingRestrictedUntilUtc",
                schema: "identity",
                table: "Users",
                type: "datetime2(7)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicClassId",
                schema: "messaging",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunicationAgeVerifications",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerifiedBirthDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PreviousCategory = table.Column<int>(type: "int", nullable: false),
                    ProposedCategory = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationAgeVerifications", x => x.Id);
                    table.CheckConstraint("CK_CommunicationAgeVerifications_Categories", "[PreviousCategory] IN (0,1,2) AND [ProposedCategory] IN (1,2)");
                    table.CheckConstraint("CK_CommunicationAgeVerifications_Source", "([Source]=1 AND [OrganizationId] IS NOT NULL) OR ([Source]=2 AND [OrganizationId] IS NULL)");
                    table.CheckConstraint("CK_CommunicationAgeVerifications_Status", "[Status] IN (1,2)");
                    table.ForeignKey(
                        name: "FK_CommunicationAgeVerifications_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAgeVerifications_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CommunicationAgeVerifications_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationInvitations",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationInvitations", x => x.Id);
                    table.CheckConstraint("CK_ConversationInvitations_Pair", "[InviterUserId] <> [TargetUserId]");
                    table.CheckConstraint("CK_ConversationInvitations_Status", "[Status] IN (1,2,3,4,5)");
                    table.CheckConstraint("CK_ConversationInvitations_Times", "[ExpiresAtUtc] > [CreatedAtUtc] AND (([Status]=1 AND [CompletedAtUtc] IS NULL) OR ([Status]<>1 AND [CompletedAtUtc] IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_ConversationInvitations_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationInvitations_Users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationInvitations_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_AgeVerificationStatus",
                schema: "identity",
                table: "Users",
                sql: "[CommunicationAgeVerificationStatus] IN (0,1,2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6, 7, 8)");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizationId_AcademicClassId",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "OrganizationId", "AcademicClassId" },
                unique: true,
                filter: "[AcademicClassId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_AcademicClass",
                schema: "messaging",
                table: "Conversations",
                sql: "[AcademicClassId] IS NULL OR ([ManagementPolicy]=2 AND [Type]=2 AND [Scope]=2 AND [OrganizationId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAgeVerifications_ActorUserId",
                schema: "identity",
                table: "CommunicationAgeVerifications",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAgeVerifications_OrganizationId",
                schema: "identity",
                table: "CommunicationAgeVerifications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationAgeVerifications_UserId_CreatedAtUtc_Id",
                schema: "identity",
                table: "CommunicationAgeVerifications",
                columns: new[] { "UserId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInvitations_ConversationId_TargetUserId",
                schema: "messaging",
                table: "ConversationInvitations",
                columns: new[] { "ConversationId", "TargetUserId" },
                unique: true,
                filter: "[Status]=1");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInvitations_InviterUserId",
                schema: "messaging",
                table: "ConversationInvitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInvitations_TargetUserId_Status_CreatedAtUtc",
                schema: "messaging",
                table: "ConversationInvitations",
                columns: new[] { "TargetUserId", "Status", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Classes_OrganizationId_AcademicClassId",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "OrganizationId", "AcademicClassId" },
                principalSchema: "academic",
                principalTable: "Classes",
                principalColumns: new[] { "OrganizationId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Classes_OrganizationId_AcademicClassId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropTable(
                name: "CommunicationAgeVerifications",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "ConversationInvitations",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_AgeVerificationStatus",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrganizationId_AcademicClassId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_AcademicClass",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "CommunicationAgeVerificationStatus",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MessagingRestrictedUntilUtc",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AcademicClassId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ModerationActions_Type",
                schema: "messaging",
                table: "ModerationActions",
                sql: "[Type] IN (1, 2, 3, 4, 5, 6)");

        }
    }
}
