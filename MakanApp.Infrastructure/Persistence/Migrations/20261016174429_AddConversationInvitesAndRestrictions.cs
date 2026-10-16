using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationInvitesAndRestrictions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.CreateTable(
                name: "ConversationInviteLinks",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatorRole = table.Column<int>(type: "int", nullable: true),
                    TokenHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    MaximumUses = table.Column<int>(type: "int", nullable: false),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationInviteLinks", x => x.Id);
                    table.UniqueConstraint("AK_ConversationInviteLinks_Id_ConversationId", x => new { x.Id, x.ConversationId });
                    table.CheckConstraint("CK_ConversationInviteLinks_CreatorContext", "([CreatorMembershipId] IS NULL AND [CreatorRole] IS NULL) OR ([CreatorMembershipId] IS NOT NULL AND [CreatorRole] IS NOT NULL AND [CreatorRole] IN (1,2,3,4))");
                    table.CheckConstraint("CK_ConversationInviteLinks_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_ConversationInviteLinks_Hash", "LEN([TokenHash]) = 64");
                    table.CheckConstraint("CK_ConversationInviteLinks_Uses", "[MaximumUses] BETWEEN 1 AND 10000 AND [UsedCount] BETWEEN 0 AND [MaximumUses]");
                    table.ForeignKey(
                        name: "FK_ConversationInviteLinks_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationInviteLinks_Memberships_CreatorMembershipId",
                        column: x => x.CreatorMembershipId,
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationInviteLinks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMemberRestrictions",
                schema: "messaging",
                columns: table => new
                {
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsBanned = table.Column<bool>(type: "bit", nullable: false),
                    DeniedCapabilities = table.Column<int>(type: "int", nullable: false),
                    UntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMemberRestrictions", x => new { x.ConversationId, x.UserId });
                    table.CheckConstraint("CK_ConversationMemberRestrictions_Capabilities", "[DeniedCapabilities] BETWEEN 0 AND 7");
                    table.CheckConstraint("CK_ConversationMemberRestrictions_Clear", "[IsBanned] = 1 OR [DeniedCapabilities] <> 0 OR [UntilUtc] IS NULL");
                    table.CheckConstraint("CK_ConversationMemberRestrictions_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_ConversationMemberRestrictions_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationMemberRestrictions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationJoinRequests",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InviteLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterMembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequesterRole = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationJoinRequests", x => x.Id);
                    table.CheckConstraint("CK_ConversationJoinRequests_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_ConversationJoinRequests_Lifecycle", "([Status] = 1 AND [CompletedAtUtc] IS NULL) OR ([Status] <> 1 AND [CompletedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ConversationJoinRequests_RequesterContext", "([RequesterMembershipId] IS NULL AND [RequesterRole] IS NULL) OR ([RequesterMembershipId] IS NOT NULL AND [RequesterRole] IS NOT NULL AND [RequesterRole] IN (1,2,3,4))");
                    table.CheckConstraint("CK_ConversationJoinRequests_Status", "[Status] IN (1,2,3,4,5)");
                    table.ForeignKey(
                        name: "FK_ConversationJoinRequests_ConversationInviteLinks_InviteLinkId_ConversationId",
                        columns: x => new { x.InviteLinkId, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "ConversationInviteLinks",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationJoinRequests_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationJoinRequests_Memberships_RequesterMembershipId",
                        column: x => x.RequesterMembershipId,
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationJoinRequests_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[AdminPermissions] BETWEEN 0 AND 4095 AND (([Role] = 2) OR [AdminPermissions] = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInviteLinks_ConversationId_CreatedAtUtc_Id",
                schema: "messaging",
                table: "ConversationInviteLinks",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInviteLinks_CreatedByUserId",
                schema: "messaging",
                table: "ConversationInviteLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInviteLinks_CreatorMembershipId",
                schema: "messaging",
                table: "ConversationInviteLinks",
                column: "CreatorMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationInviteLinks_TokenHash",
                schema: "messaging",
                table: "ConversationInviteLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationJoinRequests_ConversationId_CreatedAtUtc_Id",
                schema: "messaging",
                table: "ConversationJoinRequests",
                columns: new[] { "ConversationId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationJoinRequests_ConversationId_UserId",
                schema: "messaging",
                table: "ConversationJoinRequests",
                columns: new[] { "ConversationId", "UserId" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationJoinRequests_InviteLinkId_ConversationId",
                schema: "messaging",
                table: "ConversationJoinRequests",
                columns: new[] { "InviteLinkId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationJoinRequests_RequesterMembershipId",
                schema: "messaging",
                table: "ConversationJoinRequests",
                column: "RequesterMembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationJoinRequests_UserId_CreatedAtUtc_Id",
                schema: "messaging",
                table: "ConversationJoinRequests",
                columns: new[] { "UserId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemberRestrictions_UserId",
                schema: "messaging",
                table: "ConversationMemberRestrictions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // کاهش بی‌اجازهٔ permission داده‌های واقعی، راه rollback نیست.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [messaging].[ConversationParticipants] WHERE ([AdminPermissions] & ~511) <> 0) THROW 51000, N'پیش از rollback، permissionهای جدید گفتگو باید بازبینی شوند.', 1;");
            migrationBuilder.DropTable(
                name: "ConversationJoinRequests",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationMemberRestrictions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ConversationInviteLinks",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[AdminPermissions] BETWEEN 0 AND 511 AND (([Role] = 2) OR [AdminPermissions] = 0)");
        }
    }
}
