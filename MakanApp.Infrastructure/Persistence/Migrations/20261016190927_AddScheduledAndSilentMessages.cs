using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledAndSilentMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSilent",
                schema: "messaging",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ScheduledMessages",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceType = table.Column<int>(type: "int", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<int>(type: "int", nullable: true),
                    SubjectOrganizationPersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ReplyToMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContactUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsSilent = table.Column<bool>(type: "bit", nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DispatchedMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClaimedUntilUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    LastFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledMessages", x => x.Id);
                    table.CheckConstraint("CK_ScheduledMessages_Attempts", "[AttemptCount] BETWEEN 0 AND 5");
                    table.CheckConstraint("CK_ScheduledMessages_Context", "([WorkspaceType]=1 AND [OrganizationId] IS NULL AND [MembershipId] IS NULL AND [Role] IS NULL AND [SubjectOrganizationPersonId] IS NULL) OR ([WorkspaceType]=2 AND [OrganizationId] IS NOT NULL AND [MembershipId] IS NOT NULL AND [Role] IS NOT NULL AND [Role] IN (1,2,3,4))");
                    table.CheckConstraint("CK_ScheduledMessages_Hash", "DATALENGTH([PayloadHash]) = 32");
                    table.CheckConstraint("CK_ScheduledMessages_Kind", "[Kind] IN (1,2,3,4,5,6)");
                    table.CheckConstraint("CK_ScheduledMessages_Lease", "([ClaimToken] IS NULL AND [ClaimedUntilUtc] IS NULL) OR ([Status]=1 AND [ClaimToken] IS NOT NULL AND [ClaimedUntilUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ScheduledMessages_Lifecycle", "([Status]=1 AND [CompletedAtUtc] IS NULL AND [DispatchedMessageId] IS NULL) OR ([Status]=2 AND [CompletedAtUtc] IS NOT NULL AND [DispatchedMessageId] IS NOT NULL) OR ([Status] IN (3,4) AND [CompletedAtUtc] IS NOT NULL AND [DispatchedMessageId] IS NULL)");
                    table.CheckConstraint("CK_ScheduledMessages_Status", "[Status] IN (1,2,3,4)");
                    table.CheckConstraint("CK_ScheduledMessages_UTCOrder", "[ScheduledAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_ConversationTopics_TopicId_ConversationId",
                        columns: x => new { x.TopicId, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "ConversationTopics",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Memberships_MembershipId",
                        column: x => x.MembershipId,
                        principalSchema: "organization",
                        principalTable: "Memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Messages_DispatchedMessageId_ConversationId",
                        columns: x => new { x.DispatchedMessageId, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Messages_ReplyToMessageId_ConversationId",
                        columns: x => new { x.ReplyToMessageId, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_OrganizationPersons_SubjectOrganizationPersonId",
                        column: x => x.SubjectOrganizationPersonId,
                        principalSchema: "organization",
                        principalTable: "OrganizationPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Users_ContactUserId",
                        column: x => x.ContactUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessages_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledMessageAttachments",
                schema: "messaging",
                columns: table => new
                {
                    ScheduledMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledMessageAttachments", x => new { x.ScheduledMessageId, x.FileAssetId });
                    table.ForeignKey(
                        name: "FK_ScheduledMessageAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessageAttachments_ScheduledMessages_ScheduledMessageId",
                        column: x => x.ScheduledMessageId,
                        principalSchema: "messaging",
                        principalTable: "ScheduledMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledMessageMentions",
                schema: "messaging",
                columns: table => new
                {
                    ScheduledMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledMessageMentions", x => new { x.ScheduledMessageId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ScheduledMessageMentions_ScheduledMessages_ScheduledMessageId",
                        column: x => x.ScheduledMessageId,
                        principalSchema: "messaging",
                        principalTable: "ScheduledMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledMessageMentions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessageAttachments_FileAssetId",
                schema: "messaging",
                table: "ScheduledMessageAttachments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessageMentions_UserId",
                schema: "messaging",
                table: "ScheduledMessageMentions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_ContactUserId",
                schema: "messaging",
                table: "ScheduledMessages",
                column: "ContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_ConversationId_UserId_ClientMessageId",
                schema: "messaging",
                table: "ScheduledMessages",
                columns: new[] { "ConversationId", "UserId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_ConversationId_UserId_Status",
                schema: "messaging",
                table: "ScheduledMessages",
                columns: new[] { "ConversationId", "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_DispatchedMessageId_ConversationId",
                schema: "messaging",
                table: "ScheduledMessages",
                columns: new[] { "DispatchedMessageId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_MembershipId",
                schema: "messaging",
                table: "ScheduledMessages",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_OrganizationId",
                schema: "messaging",
                table: "ScheduledMessages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_ReplyToMessageId_ConversationId",
                schema: "messaging",
                table: "ScheduledMessages",
                columns: new[] { "ReplyToMessageId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_Status_ScheduledAtUtc_Id",
                schema: "messaging",
                table: "ScheduledMessages",
                columns: new[] { "Status", "ScheduledAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_SubjectOrganizationPersonId",
                schema: "messaging",
                table: "ScheduledMessages",
                column: "SubjectOrganizationPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_TopicId_ConversationId",
                schema: "messaging",
                table: "ScheduledMessages",
                columns: new[] { "TopicId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledMessages_UserId",
                schema: "messaging",
                table: "ScheduledMessages",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // قصد پایدار و معنای بی‌صدای پیام واقعی در rollback بی‌اجازه از دست نمی‌روند.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [messaging].[ScheduledMessages]) OR EXISTS (SELECT 1 FROM [messaging].[Messages] WHERE [IsSilent]=1) THROW 51000, N'پیش از rollback زمان‌بندی/بی‌صدا، backup و تصمیم عملیاتی لازم است.', 1;");
            migrationBuilder.DropTable(
                name: "ScheduledMessageAttachments",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ScheduledMessageMentions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ScheduledMessages",
                schema: "messaging");

            migrationBuilder.DropColumn(
                name: "IsSilent",
                schema: "messaging",
                table: "Messages");
        }
    }
}
