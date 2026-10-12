using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingModerationAndChildSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AbuseReports_Reason",
                schema: "messaging",
                table: "AbuseReports");

            migrationBuilder.CreateTable(
                name: "SafetyIncidents",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentKind = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PolicyCodes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ContentSnapshot = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ContentHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    RequestPayloadHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyIncidents", x => x.Id);
                    table.CheckConstraint("CK_SafetyIncidents_ContentKind", "[ContentKind] IN (1, 2, 3, 4, 5)");
                    table.CheckConstraint("CK_SafetyIncidents_Decision", "[Decision] IN (2, 3)");
                    table.CheckConstraint("CK_SafetyIncidents_Lifecycle", "([Status] = 1 AND [ClosedAtUtc] IS NULL) OR ([Status] IN (2, 3) AND [ClosedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_SafetyIncidents_Status", "[Status] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_SafetyIncidents_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyIncidents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationCases",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    AbuseReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SafetyIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QueueScope = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InternalNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ReviewStartedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DismissedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationCases", x => x.Id);
                    table.CheckConstraint("CK_ModerationCases_Lifecycle", "([Status] = 1 AND [AssignedReviewerUserId] IS NULL AND [ReviewStartedAtUtc] IS NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NULL) OR ([Status] = 2 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NULL) OR ([Status] = 3 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NOT NULL AND [DismissedAtUtc] IS NULL) OR ([Status] = 4 AND [AssignedReviewerUserId] IS NOT NULL AND [ReviewStartedAtUtc] IS NOT NULL AND [ResolvedAtUtc] IS NULL AND [DismissedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ModerationCases_Priority", "[Priority] IN (1, 2, 3)");
                    table.CheckConstraint("CK_ModerationCases_QueueScope", "[QueueScope] IN (1, 2)");
                    table.CheckConstraint("CK_ModerationCases_Source", "[Source] IN (1, 2)");
                    table.CheckConstraint("CK_ModerationCases_SourceShape", "([Source] = 1 AND [AbuseReportId] IS NOT NULL AND [SafetyIncidentId] IS NULL) OR ([Source] = 2 AND [AbuseReportId] IS NULL AND [SafetyIncidentId] IS NOT NULL)");
                    table.CheckConstraint("CK_ModerationCases_Status", "[Status] IN (1, 2, 3, 4)");
                    table.ForeignKey(
                        name: "FK_ModerationCases_AbuseReports_AbuseReportId",
                        column: x => x.AbuseReportId,
                        principalSchema: "messaging",
                        principalTable: "AbuseReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationCases_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationCases_SafetyIncidents_SafetyIncidentId",
                        column: x => x.SafetyIncidentId,
                        principalSchema: "messaging",
                        principalTable: "SafetyIncidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationCases_Users_AssignedReviewerUserId",
                        column: x => x.AssignedReviewerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActions",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModerationCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActions", x => x.Id);
                    table.CheckConstraint("CK_ModerationActions_Type", "[Type] IN (1, 2, 3, 4, 5)");
                    table.ForeignKey(
                        name: "FK_ModerationActions_ModerationCases_ModerationCaseId",
                        column: x => x.ModerationCaseId,
                        principalSchema: "messaging",
                        principalTable: "ModerationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationEvidenceAttachments",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModerationCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationEvidenceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationEvidenceAttachments_FileAssets_FileAssetId",
                        column: x => x.FileAssetId,
                        principalSchema: "storage",
                        principalTable: "FileAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationEvidenceAttachments_ModerationCases_ModerationCaseId",
                        column: x => x.ModerationCaseId,
                        principalSchema: "messaging",
                        principalTable: "ModerationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [messaging].[ModerationCases]
                    ([Id], [ConversationId], [Source], [AbuseReportId], [SafetyIncidentId],
                     [QueueScope], [Priority], [Status], [AssignedReviewerUserId], [InternalNote],
                     [CreatedAtUtc], [UpdatedAtUtc], [ReviewStartedAtUtc], [ResolvedAtUtc], [DismissedAtUtc])
                SELECT report.[Id], report.[ConversationId], 1, report.[Id], NULL,
                       CASE
                           WHEN report.[Reason] = 7 OR conversation.[Type] = 1 OR conversation.[ManagementPolicy] = 2
                               THEN 2
                           ELSE 1
                       END,
                       CASE WHEN report.[Reason] = 7 THEN 3 ELSE 1 END,
                       1, NULL, NULL, report.[CreatedAtUtc], report.[CreatedAtUtc], NULL, NULL, NULL
                FROM [messaging].[AbuseReports] AS report
                INNER JOIN [messaging].[Conversations] AS conversation
                    ON conversation.[Id] = report.[ConversationId];

                INSERT INTO [messaging].[ModerationActions]
                    ([Id], [ModerationCaseId], [Type], [ActorUserId], [CreatedAtUtc])
                SELECT report.[Id], moderationCase.[Id], 1, report.[ReporterUserId], report.[CreatedAtUtc]
                FROM [messaging].[ModerationCases] AS moderationCase
                INNER JOIN [messaging].[AbuseReports] AS report
                    ON report.[Id] = moderationCase.[AbuseReportId];

                INSERT INTO [messaging].[ModerationEvidenceAttachments]
                    ([Id], [ModerationCaseId], [FileAssetId], [CreatedAtUtc])
                SELECT CONVERT(uniqueidentifier, SUBSTRING(HASHBYTES(
                           'SHA2_256',
                           CONVERT(binary(16), moderationCase.[Id]) +
                           CONVERT(binary(16), attachment.[FileAssetId])), 1, 16)),
                       moderationCase.[Id], attachment.[FileAssetId], report.[CreatedAtUtc]
                FROM [messaging].[ModerationCases] AS moderationCase
                INNER JOIN [messaging].[AbuseReports] AS report
                    ON report.[Id] = moderationCase.[AbuseReportId]
                INNER JOIN [messaging].[MessageAttachments] AS attachment
                    ON attachment.[MessageId] = report.[MessageId];

                UPDATE fileAsset
                SET [RetainedAtUtc] = COALESCE(fileAsset.[RetainedAtUtc], evidence.[CreatedAtUtc])
                FROM [storage].[FileAssets] AS fileAsset
                INNER JOIN [messaging].[ModerationEvidenceAttachments] AS evidence
                    ON evidence.[FileAssetId] = fileAsset.[Id];
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[AdminPermissions] BETWEEN 0 AND 511 AND (([Role] = 2) OR [AdminPermissions] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AbuseReports_Reason",
                schema: "messaging",
                table: "AbuseReports",
                sql: "[Reason] IN (1, 2, 3, 4, 5, 6, 7)");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_ActorUserId",
                schema: "messaging",
                table: "ModerationActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_Case_Created",
                schema: "messaging",
                table: "ModerationActions",
                columns: new[] { "ModerationCaseId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_Assigned_Status",
                schema: "messaging",
                table: "ModerationCases",
                columns: new[] { "AssignedReviewerUserId", "Status" },
                filter: "[AssignedReviewerUserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_Conversation_Status_Created",
                schema: "messaging",
                table: "ModerationCases",
                columns: new[] { "ConversationId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_Queue_Status_Priority_Created",
                schema: "messaging",
                table: "ModerationCases",
                columns: new[] { "QueueScope", "Status", "Priority", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_ModerationCases_AbuseReportId",
                schema: "messaging",
                table: "ModerationCases",
                column: "AbuseReportId",
                unique: true,
                filter: "[AbuseReportId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ModerationCases_SafetyIncidentId",
                schema: "messaging",
                table: "ModerationCases",
                column: "SafetyIncidentId",
                unique: true,
                filter: "[SafetyIncidentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationEvidenceAttachments_FileAssetId",
                schema: "messaging",
                table: "ModerationEvidenceAttachments",
                column: "FileAssetId");

            migrationBuilder.CreateIndex(
                name: "UX_ModerationEvidenceAttachments_Case_File",
                schema: "messaging",
                table: "ModerationEvidenceAttachments",
                columns: new[] { "ModerationCaseId", "FileAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyIncidents_ActorUserId",
                schema: "messaging",
                table: "SafetyIncidents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SafetyIncidents_Conversation_Created",
                schema: "messaging",
                table: "SafetyIncidents",
                columns: new[] { "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_SafetyIncidents_Conversation_Actor_ClientMessageId",
                schema: "messaging",
                table: "SafetyIncidents",
                columns: new[] { "ConversationId", "ActorUserId", "ClientMessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModerationActions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ModerationEvidenceAttachments",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ModerationCases",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "SafetyIncidents",
                schema: "messaging");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AbuseReports_Reason",
                schema: "messaging",
                table: "AbuseReports");

            migrationBuilder.Sql(
                "UPDATE [messaging].[ConversationParticipants] SET [AdminPermissions] = [AdminPermissions] & 255 WHERE [AdminPermissions] > 255;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_AdminPermissions",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "[AdminPermissions] BETWEEN 0 AND 255 AND (([Role] = 2) OR [AdminPermissions] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AbuseReports_Reason",
                schema: "messaging",
                table: "AbuseReports",
                sql: "[Reason] IN (1, 2, 3, 4, 5, 6)");
        }
    }
}
