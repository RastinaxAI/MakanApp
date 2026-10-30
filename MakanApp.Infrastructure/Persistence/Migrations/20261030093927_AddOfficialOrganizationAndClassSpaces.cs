using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialOrganizationAndClassSpaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrganizationId_AcademicClassId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_AcademicClass",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_Lifecycle",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.AddColumn<Guid>(
                name: "AcademicActivityPublicationId",
                schema: "messaging",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOfficialOrganizationSpace",
                schema: "messaging",
                table: "Conversations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EndedBySystem",
                schema: "messaging",
                table: "ConversationParticipants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_AssignmentVersions_OrganizationId_Id",
                schema: "assessment",
                table: "AssignmentVersions",
                columns: new[] { "OrganizationId", "Id" });

            migrationBuilder.CreateTable(
                name: "AcademicActivityPublications",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignmentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceRevision = table.Column<int>(type: "int", nullable: false),
                    InitiatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailableFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailableUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicActivityPublications", x => x.Id);
                    table.CheckConstraint("CK_AcademicActivityPublications_Revision", "[SourceRevision]>0");
                    table.CheckConstraint("CK_AcademicActivityPublications_Source", "([Kind]=1 AND [LessonId] IS NOT NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NULL) OR ([Kind]=2 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NOT NULL AND [ExamVersionId] IS NULL) OR ([Kind]=3 AND [LessonId] IS NULL AND [AssignmentVersionId] IS NULL AND [ExamVersionId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AcademicActivityPublications_AssignmentVersions_OrganizationId_AssignmentVersionId",
                        columns: x => new { x.OrganizationId, x.AssignmentVersionId },
                        principalSchema: "assessment",
                        principalTable: "AssignmentVersions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicActivityPublications_ClassLessons_OrganizationId_LessonId",
                        columns: x => new { x.OrganizationId, x.LessonId },
                        principalSchema: "academic",
                        principalTable: "ClassLessons",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicActivityPublications_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicActivityPublications_ExamVersions_OrganizationId_ExamVersionId",
                        columns: x => new { x.OrganizationId, x.ExamVersionId },
                        principalSchema: "assessment",
                        principalTable: "ExamVersions",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicActivityPublications_Users_InitiatedByUserId",
                        column: x => x.InitiatedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfficialSpaceProvisions",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialSpaceProvisions", x => x.Id);
                    table.CheckConstraint("CK_OfficialSpaceProvisions_Attempt", "[AttemptCount]>=0");
                    table.CheckConstraint("CK_OfficialSpaceProvisions_Purpose", "[Purpose] IN (2,3)");
                    table.CheckConstraint("CK_OfficialSpaceProvisions_State", "[State] IN (1,2,3)");
                    table.ForeignKey(
                        name: "FK_OfficialSpaceProvisions_Classes_OrganizationId_ClassId",
                        columns: x => new { x.OrganizationId, x.ClassId },
                        principalSchema: "academic",
                        principalTable: "Classes",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfficialSpaceProvisions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalSchema: "organization",
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademicActivityDeliveries",
                schema: "messaging",
                columns: table => new
                {
                    PublicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<int>(type: "int", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailureCategory = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicActivityDeliveries", x => new { x.PublicationId, x.Purpose });
                    table.CheckConstraint("CK_AcademicActivityDeliveries_Attempt", "[AttemptCount]>=0");
                    table.CheckConstraint("CK_AcademicActivityDeliveries_Purpose", "[Purpose] IN (2,3)");
                    table.CheckConstraint("CK_AcademicActivityDeliveries_State", "([State] IN (1,3) AND [MessageId] IS NULL AND [ConversationId] IS NULL) OR ([State]=2 AND [MessageId] IS NOT NULL AND [ConversationId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_AcademicActivityDeliveries_AcademicActivityPublications_PublicationId",
                        column: x => x.PublicationId,
                        principalSchema: "messaging",
                        principalTable: "AcademicActivityPublications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicActivityDeliveries_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "messaging",
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AcademicActivityDeliveries_Messages_MessageId_ConversationId",
                        columns: x => new { x.MessageId, x.ConversationId },
                        principalSchema: "messaging",
                        principalTable: "Messages",
                        principalColumns: new[] { "Id", "ConversationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AcademicActivityPublicationId_ConversationId",
                schema: "messaging",
                table: "Messages",
                columns: new[] { "AcademicActivityPublicationId", "ConversationId" },
                unique: true,
                filter: "[AcademicActivityPublicationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizationId_AcademicClassId_Type",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "OrganizationId", "AcademicClassId", "Type" },
                unique: true,
                filter: "[AcademicClassId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizationId_Type",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "OrganizationId", "Type" },
                unique: true,
                filter: "[IsOfficialOrganizationSpace]=1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_AcademicClass",
                schema: "messaging",
                table: "Conversations",
                sql: "[AcademicClassId] IS NULL OR ([ManagementPolicy]=2 AND [Type] IN (2,3) AND [Scope]=2 AND [OrganizationId] IS NOT NULL AND [IsOfficialOrganizationSpace]=0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_OfficialOrganization",
                schema: "messaging",
                table: "Conversations",
                sql: "[IsOfficialOrganizationSpace]=0 OR ([ManagementPolicy]=2 AND [Type] IN (2,3) AND [Scope]=2 AND [OrganizationId] IS NOT NULL AND [AcademicClassId] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_Lifecycle",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "([Status] = 1 AND [EndedAtUtc] IS NULL AND [EndedByUserId] IS NULL AND [EndedBySystem]=0) OR ([Status] IN (2, 3) AND [EndedAtUtc] IS NOT NULL AND (([EndedByUserId] IS NOT NULL AND [EndedBySystem]=0) OR ([Status]=2 AND [EndedByUserId] IS NULL AND [EndedBySystem]=1)))");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityDeliveries_ConversationId",
                schema: "messaging",
                table: "AcademicActivityDeliveries",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityDeliveries_MessageId",
                schema: "messaging",
                table: "AcademicActivityDeliveries",
                column: "MessageId",
                unique: true,
                filter: "[MessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityDeliveries_MessageId_ConversationId",
                schema: "messaging",
                table: "AcademicActivityDeliveries",
                columns: new[] { "MessageId", "ConversationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityDeliveries_PublicationId_ConversationId",
                schema: "messaging",
                table: "AcademicActivityDeliveries",
                columns: new[] { "PublicationId", "ConversationId" },
                unique: true,
                filter: "[ConversationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                column: "AssignmentVersionId",
                unique: true,
                filter: "[AssignmentVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                column: "ExamVersionId",
                unique: true,
                filter: "[ExamVersionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_InitiatedByUserId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_LessonId_SourceRevision",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "LessonId", "SourceRevision" },
                unique: true,
                filter: "[LessonId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_AssignmentVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "AssignmentVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ClassId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_ExamVersionId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "ExamVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicActivityPublications_OrganizationId_LessonId",
                schema: "messaging",
                table: "AcademicActivityPublications",
                columns: new[] { "OrganizationId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSpaceProvisions_OrganizationId_ClassId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                columns: new[] { "OrganizationId", "ClassId", "Purpose" },
                unique: true,
                filter: "[ClassId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSpaceProvisions_OrganizationId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                columns: new[] { "OrganizationId", "Purpose" },
                unique: true,
                filter: "[ClassId] IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_AcademicActivityPublications_AcademicActivityPublicationId",
                schema: "messaging",
                table: "Messages",
                column: "AcademicActivityPublicationId",
                principalSchema: "messaging",
                principalTable: "AcademicActivityPublications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // پس از استفاده از قابلیت، rollback اسکیمای قدیمی می‌تواند binding و سابقهٔ تحویل را از بین ببرد.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [messaging].[OfficialSpaceProvisions])
                   OR EXISTS (SELECT 1 FROM [messaging].[AcademicActivityPublications])
                   OR EXISTS (SELECT 1 FROM [messaging].[AcademicActivityDeliveries])
                   OR EXISTS (SELECT 1 FROM [messaging].[Conversations] WHERE [IsOfficialOrganizationSpace]=1 OR ([AcademicClassId] IS NOT NULL AND [Type]=3))
                   OR EXISTS (SELECT 1 FROM [messaging].[ConversationParticipants] WHERE [EndedBySystem]=1)
                    THROW 51131, N'بازگشت پس از استفاده از فضاهای رسمی نیازمند برنامهٔ حفظ داده و تأیید مستقل است.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_AcademicActivityPublications_AcademicActivityPublicationId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "AcademicActivityDeliveries",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "OfficialSpaceProvisions",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "AcademicActivityPublications",
                schema: "messaging");

            migrationBuilder.DropIndex(
                name: "IX_Messages_AcademicActivityPublicationId_ConversationId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrganizationId_AcademicClassId_Type",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_OrganizationId_Type",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_AcademicClass",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_OfficialOrganization",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ConversationParticipants_Lifecycle",
                schema: "messaging",
                table: "ConversationParticipants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_AssignmentVersions_OrganizationId_Id",
                schema: "assessment",
                table: "AssignmentVersions");

            migrationBuilder.DropColumn(
                name: "AcademicActivityPublicationId",
                schema: "messaging",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsOfficialOrganizationSpace",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "EndedBySystem",
                schema: "messaging",
                table: "ConversationParticipants");

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

            migrationBuilder.AddCheckConstraint(
                name: "CK_ConversationParticipants_Lifecycle",
                schema: "messaging",
                table: "ConversationParticipants",
                sql: "([Status] = 1 AND [EndedAtUtc] IS NULL AND [EndedByUserId] IS NULL) OR ([Status] IN (2, 3) AND [EndedAtUtc] IS NOT NULL AND [EndedByUserId] IS NOT NULL)");
        }
    }
}
