using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewedPersonalClassLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalClassParticipants_InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropIndex(
                name: "IX_OfficialSpaceProvisions_OrganizationId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_SystemManagedAcademic",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "DelegatedCapabilities",
                schema: "academic",
                table: "PersonalClassParticipants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                schema: "academic",
                table: "PersonalClassParticipants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocalRoles",
                schema: "academic",
                table: "PersonalClassParticipants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                schema: "academic",
                table: "PersonalClassInvitations",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeclinedAtUtc",
                schema: "academic",
                table: "PersonalClassInvitations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptingJoinRequests",
                schema: "academic",
                table: "PersonalClasses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                schema: "academic",
                table: "PersonalClasses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FormationRequestId",
                schema: "academic",
                table: "PersonalClasses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerUserId",
                schema: "academic",
                table: "PersonalClasses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MigrationState",
                schema: "academic",
                table: "PersonalClasses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "State",
                schema: "academic",
                table: "PersonalClasses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PersonalClassId",
                schema: "messaging",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            // فقط نگاشت رابطهٔ موجود؛ ظرفیت، قرارداد تجاری و پیام تاریخی ساخته نمی‌شود.
            migrationBuilder.Sql("""
                UPDATE [academic].[PersonalClassParticipants] SET [LocalRoles]=1;
                UPDATE c SET [State]=CASE WHEN c.[ArchivedAtUtc] IS NULL THEN 3 ELSE 6 END,
                    [MigrationState]=CASE WHEN u.[AccountStatus]<>1 THEN 3 WHEN c.[ArchivedAtUtc] IS NULL THEN 1 ELSE 2 END,
                    [ManagerUserId]=CASE WHEN u.[AccountStatus]=1 THEN c.[HostUserId] ELSE NULL END
                FROM [academic].[PersonalClasses] c INNER JOIN [identity].[Users] u ON u.[Id]=c.[HostUserId];
                UPDATE m SET [LocalRoles]=[LocalRoles]|4
                FROM [academic].[PersonalClassParticipants] m INNER JOIN [academic].[PersonalClasses] c
                    ON c.[Id]=m.[PersonalClassId] AND c.[ManagerUserId]=m.[StudentUserId]
                WHERE m.[EndedAtUtc] IS NULL;
                INSERT INTO [academic].[PersonalClassParticipants]
                    ([Id],[PersonalClassId],[StudentUserId],[InvitationId],[JoinedAtUtc],[EndedAtUtc],[LocalRoles],[DelegatedCapabilities],[EndReason])
                SELECT NEWID(),c.[Id],c.[ManagerUserId],NULL,c.[CreatedAtUtc],NULL,4,0,NULL
                FROM [academic].[PersonalClasses] c
                WHERE c.[ManagerUserId] IS NOT NULL AND NOT EXISTS
                    (SELECT 1 FROM [academic].[PersonalClassParticipants] m WHERE m.[PersonalClassId]=c.[Id]
                     AND m.[StudentUserId]=c.[ManagerUserId] AND m.[EndedAtUtc] IS NULL);
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PersonalClassParticipants_PersonalClassId_StudentUserId_Id",
                schema: "academic",
                table: "PersonalClassParticipants",
                columns: new[] { "PersonalClassId", "StudentUserId", "Id" });

            migrationBuilder.CreateTable(
                name: "PersonalClassJoinRequests",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MembershipId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalClassJoinRequests", x => x.Id);
                    table.CheckConstraint("CK_PersonalClassJoinRequests_Closure", "([Status]=1 AND [ClosedAtUtc] IS NULL AND [ClosureReason] IS NULL) OR ([Status]<>1 AND [ClosedAtUtc] IS NOT NULL AND [ClosedAtUtc]>=[CreatedAtUtc] AND [ClosureReason] IS NOT NULL)");
                    table.CheckConstraint("CK_PersonalClassJoinRequests_Membership", "([Status]=2 AND [MembershipId] IS NOT NULL) OR ([Status]<>2 AND [MembershipId] IS NULL)");
                    table.CheckConstraint("CK_PersonalClassJoinRequests_Status", "[Status] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_PersonalClassJoinRequests_PersonalClassParticipants_PersonalClassId_UserId_MembershipId",
                        columns: x => new { x.PersonalClassId, x.UserId, x.MembershipId },
                        principalSchema: "academic",
                        principalTable: "PersonalClassParticipants",
                        principalColumns: new[] { "PersonalClassId", "StudentUserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassJoinRequests_PersonalClasses_PersonalClassId",
                        column: x => x.PersonalClassId,
                        principalSchema: "academic",
                        principalTable: "PersonalClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassJoinRequests_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalClassManagerTransfers",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonalClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalClassManagerTransfers", x => x.Id);
                    table.CheckConstraint("CK_PersonalClassManagerTransfers_Decision", "([Status]=1 AND [DecidedAtUtc] IS NULL) OR ([Status]<>1 AND [DecidedAtUtc] IS NOT NULL AND [DecidedAtUtc]>=[CreatedAtUtc])");
                    table.CheckConstraint("CK_PersonalClassManagerTransfers_Status", "[Status] BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_PersonalClassManagerTransfers_Target", "[PreviousManagerUserId] IS NULL OR [PreviousManagerUserId]<>[TargetUserId]");
                    table.ForeignKey(
                        name: "FK_PersonalClassManagerTransfers_PersonalClasses_PersonalClassId",
                        column: x => x.PersonalClassId,
                        principalSchema: "academic",
                        principalTable: "PersonalClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassManagerTransfers_Users_PreviousManagerUserId",
                        column: x => x.PreviousManagerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassManagerTransfers_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassManagerTransfers_Users_TargetUserId",
                        column: x => x.TargetUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalClassRequests",
                schema: "academic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequesterUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreationPayloadHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SuggestedCapacity = table.Column<int>(type: "int", nullable: false),
                    SuggestedManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SuggestedTeacherUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FinalManagerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalClassRequests", x => x.Id);
                    table.CheckConstraint("CK_PersonalClassRequests_Approval", "([Status]=5 AND [CreatedClassId] IS NOT NULL AND [FinalManagerUserId] IS NOT NULL) OR ([Status]<>5 AND [CreatedClassId] IS NULL AND [FinalManagerUserId] IS NULL)");
                    table.CheckConstraint("CK_PersonalClassRequests_Capacity", "[SuggestedCapacity]>0");
                    table.CheckConstraint("CK_PersonalClassRequests_Status", "[Status] BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_PersonalClassRequests_PersonalClasses_CreatedClassId",
                        column: x => x.CreatedClassId,
                        principalSchema: "academic",
                        principalTable: "PersonalClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassRequests_Users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassRequests_Users_FinalManagerUserId",
                        column: x => x.FinalManagerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassRequests_Users_RequesterUserId",
                        column: x => x.RequesterUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassRequests_Users_SuggestedManagerUserId",
                        column: x => x.SuggestedManagerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalClassRequests_Users_SuggestedTeacherUserId",
                        column: x => x.SuggestedTeacherUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformPermissions",
                columns: new[] { "Id", "Description", "Key" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000027"), "مشاهده درخواست کلاس مستقل", "platform.personal_classes.read" },
                    { new Guid("20000000-0000-0000-0000-000000000028"), "بررسی درخواست کلاس مستقل", "platform.personal_classes.review" }
                });

            migrationBuilder.InsertData(
                schema: "platform",
                table: "PlatformRolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000027"), new Guid("10000000-0000-0000-0000-000000000001") },
                    { new Guid("20000000-0000-0000-0000-000000000028"), new Guid("10000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassParticipants_InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants",
                column: "InvitationId",
                unique: true,
                filter: "[InvitationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_PersonalClassParticipants_PrimaryManager",
                schema: "academic",
                table: "PersonalClassParticipants",
                column: "PersonalClassId",
                unique: true,
                filter: "[EndedAtUtc] IS NULL AND [LocalRoles]>=4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClassParticipants_Delegation",
                schema: "academic",
                table: "PersonalClassParticipants",
                sql: "[DelegatedCapabilities] BETWEEN 0 AND 3 AND ([DelegatedCapabilities]=0 OR ([LocalRoles]&2)=2)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClassParticipants_Roles",
                schema: "academic",
                table: "PersonalClassParticipants",
                sql: "[LocalRoles] BETWEEN 1 AND 7");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClasses_FormationRequestId",
                schema: "academic",
                table: "PersonalClasses",
                column: "FormationRequestId",
                unique: true,
                filter: "[FormationRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClasses_ManagerUserId",
                schema: "academic",
                table: "PersonalClasses",
                column: "ManagerUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClasses_Archive",
                schema: "academic",
                table: "PersonalClasses",
                sql: "([State]=6 AND [ArchivedAtUtc] IS NOT NULL) OR ([State]<>6 AND [ArchivedAtUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClasses_Capacity",
                schema: "academic",
                table: "PersonalClasses",
                sql: "[Capacity] IS NULL OR [Capacity]>0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClasses_Manager",
                schema: "academic",
                table: "PersonalClasses",
                sql: "[State]<>3 OR [ManagerUserId] IS NOT NULL OR [MigrationState]<>0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClasses_Migration",
                schema: "academic",
                table: "PersonalClasses",
                sql: "[MigrationState] BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PersonalClasses_State",
                schema: "academic",
                table: "PersonalClasses",
                sql: "[State] BETWEEN 1 AND 6");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSpaceProvisions_OrganizationId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                columns: new[] { "OrganizationId", "Purpose" },
                unique: true,
                filter: "[ClassId] IS NULL AND [OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSpaceProvisions_PersonalClassId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                columns: new[] { "PersonalClassId", "Purpose" },
                unique: true,
                filter: "[PersonalClassId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OfficialSpaceProvisions_Source",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                sql: "([PersonalClassId] IS NULL AND [OrganizationId] IS NOT NULL) OR ([PersonalClassId] IS NOT NULL AND [OrganizationId] IS NULL AND [ClassId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_PersonalClassId_Type",
                schema: "messaging",
                table: "Conversations",
                columns: new[] { "PersonalClassId", "Type" },
                unique: true,
                filter: "[PersonalClassId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_PersonalClass",
                schema: "messaging",
                table: "Conversations",
                sql: "[PersonalClassId] IS NULL OR ([ManagementPolicy]=2 AND [Type] IN (2,3) AND [Scope]=1 AND [OrganizationId] IS NULL AND [AcademicClassId] IS NULL AND [IsOfficialOrganizationSpace]=0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_SystemManagedAcademic",
                schema: "messaging",
                table: "Conversations",
                sql: "([ManagementPolicy] <> 2) OR (([Scope] = 2 OR ([Scope]=1 AND [PersonalClassId] IS NOT NULL)) AND [CreatedByUserId] IS NULL AND [ClientOperationId] IS NULL AND [CreationPayloadHash] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassJoinRequests_PersonalClassId_UserId",
                schema: "academic",
                table: "PersonalClassJoinRequests",
                columns: new[] { "PersonalClassId", "UserId" },
                unique: true,
                filter: "[Status]=1");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassJoinRequests_PersonalClassId_UserId_MembershipId",
                schema: "academic",
                table: "PersonalClassJoinRequests",
                columns: new[] { "PersonalClassId", "UserId", "MembershipId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassJoinRequests_UserId_ClientOperationId",
                schema: "academic",
                table: "PersonalClassJoinRequests",
                columns: new[] { "UserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassManagerTransfers_PersonalClassId",
                schema: "academic",
                table: "PersonalClassManagerTransfers",
                column: "PersonalClassId",
                unique: true,
                filter: "[Status]=1");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassManagerTransfers_PersonalClassId_ClientOperationId",
                schema: "academic",
                table: "PersonalClassManagerTransfers",
                columns: new[] { "PersonalClassId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassManagerTransfers_PreviousManagerUserId",
                schema: "academic",
                table: "PersonalClassManagerTransfers",
                column: "PreviousManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassManagerTransfers_RequestedByUserId",
                schema: "academic",
                table: "PersonalClassManagerTransfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassManagerTransfers_TargetUserId",
                schema: "academic",
                table: "PersonalClassManagerTransfers",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassRequests_CreatedClassId",
                schema: "academic",
                table: "PersonalClassRequests",
                column: "CreatedClassId",
                unique: true,
                filter: "[CreatedClassId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassRequests_DecidedByUserId",
                schema: "academic",
                table: "PersonalClassRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassRequests_FinalManagerUserId",
                schema: "academic",
                table: "PersonalClassRequests",
                column: "FinalManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassRequests_RequesterUserId_ClientOperationId",
                schema: "academic",
                table: "PersonalClassRequests",
                columns: new[] { "RequesterUserId", "ClientOperationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassRequests_Status_CreatedAtUtc_Id",
                schema: "academic",
                table: "PersonalClassRequests",
                columns: new[] { "Status", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassRequests_SuggestedManagerUserId",
                schema: "academic",
                table: "PersonalClassRequests",
                column: "SuggestedManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassRequests_SuggestedTeacherUserId",
                schema: "academic",
                table: "PersonalClassRequests",
                column: "SuggestedTeacherUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_PersonalClasses_PersonalClassId",
                schema: "messaging",
                table: "Conversations",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OfficialSpaceProvisions_PersonalClasses_PersonalClassId",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalClasses_PersonalClassRequests_FormationRequestId",
                schema: "academic",
                table: "PersonalClasses",
                column: "FormationRequestId",
                principalSchema: "academic",
                principalTable: "PersonalClassRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalClasses_Users_ManagerUserId",
                schema: "academic",
                table: "PersonalClasses",
                column: "ManagerUserId",
                principalSchema: "identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalClassParticipants_PersonalClasses_PersonalClassId",
                schema: "academic",
                table: "PersonalClassParticipants",
                column: "PersonalClassId",
                principalSchema: "academic",
                principalTable: "PersonalClasses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // بازگشت schema نباید مسئولیت، عضویت یا فضای مستقلِ ثبت‌شده را حذف کند.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [academic].[PersonalClassRequests])
                   OR EXISTS (SELECT 1 FROM [academic].[PersonalClassJoinRequests])
                   OR EXISTS (SELECT 1 FROM [academic].[PersonalClassManagerTransfers])
                   OR EXISTS (SELECT 1 FROM [academic].[PersonalClassParticipants] WHERE [InvitationId] IS NULL OR [LocalRoles]<>1)
                   OR EXISTS (SELECT 1 FROM [academic].[PersonalClasses] WHERE [Capacity] IS NOT NULL)
                   OR EXISTS (SELECT 1 FROM [messaging].[Conversations] WHERE [PersonalClassId] IS NOT NULL)
                   OR EXISTS (SELECT 1 FROM [messaging].[OfficialSpaceProvisions] WHERE [PersonalClassId] IS NOT NULL)
                    THROW 51160, 'Reviewed personal class data requires a reviewed data-preserving rollback.', 1;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_PersonalClasses_PersonalClassId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_OfficialSpaceProvisions_PersonalClasses_PersonalClassId",
                schema: "messaging",
                table: "OfficialSpaceProvisions");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonalClasses_PersonalClassRequests_FormationRequestId",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonalClasses_Users_ManagerUserId",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonalClassParticipants_PersonalClasses_PersonalClassId",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropTable(
                name: "PersonalClassJoinRequests",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "PersonalClassManagerTransfers",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "PersonalClassRequests",
                schema: "academic");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PersonalClassParticipants_PersonalClassId_StudentUserId_Id",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropIndex(
                name: "IX_PersonalClassParticipants_InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropIndex(
                name: "UX_PersonalClassParticipants_PrimaryManager",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClassParticipants_Delegation",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClassParticipants_Roles",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropIndex(
                name: "IX_PersonalClasses_FormationRequestId",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropIndex(
                name: "IX_PersonalClasses_ManagerUserId",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClasses_Archive",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClasses_Capacity",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClasses_Manager",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClasses_Migration",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PersonalClasses_State",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropIndex(
                name: "IX_OfficialSpaceProvisions_OrganizationId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions");

            migrationBuilder.DropIndex(
                name: "IX_OfficialSpaceProvisions_PersonalClassId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OfficialSpaceProvisions_Source",
                schema: "messaging",
                table: "OfficialSpaceProvisions");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_PersonalClassId_Type",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_PersonalClass",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Conversations_SystemManagedAcademic",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000027"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformRolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("20000000-0000-0000-0000-000000000028"), new Guid("10000000-0000-0000-0000-000000000001") });

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                schema: "platform",
                table: "PlatformPermissions",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000028"));

            migrationBuilder.DropColumn(
                name: "DelegatedCapabilities",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropColumn(
                name: "EndReason",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropColumn(
                name: "LocalRoles",
                schema: "academic",
                table: "PersonalClassParticipants");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                schema: "academic",
                table: "PersonalClassInvitations");

            migrationBuilder.DropColumn(
                name: "DeclinedAtUtc",
                schema: "academic",
                table: "PersonalClassInvitations");

            migrationBuilder.DropColumn(
                name: "AcceptingJoinRequests",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropColumn(
                name: "Capacity",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropColumn(
                name: "FormationRequestId",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropColumn(
                name: "ManagerUserId",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropColumn(
                name: "MigrationState",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropColumn(
                name: "State",
                schema: "academic",
                table: "PersonalClasses");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "messaging",
                table: "OfficialSpaceProvisions");

            migrationBuilder.DropColumn(
                name: "PersonalClassId",
                schema: "messaging",
                table: "Conversations");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "OrganizationId",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalClassParticipants_InvitationId",
                schema: "academic",
                table: "PersonalClassParticipants",
                column: "InvitationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialSpaceProvisions_OrganizationId_Purpose",
                schema: "messaging",
                table: "OfficialSpaceProvisions",
                columns: new[] { "OrganizationId", "Purpose" },
                unique: true,
                filter: "[ClassId] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Conversations_SystemManagedAcademic",
                schema: "messaging",
                table: "Conversations",
                sql: "([ManagementPolicy] <> 2) OR ([Scope] = 2 AND [CreatedByUserId] IS NULL AND [ClientOperationId] IS NULL AND [CreationPayloadHash] IS NULL)");
        }
    }
}
