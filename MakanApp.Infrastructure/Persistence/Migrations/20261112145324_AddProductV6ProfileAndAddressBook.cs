using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductV6ProfileAndAddressBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                schema: "identity",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AddressBookEntries",
                schema: "messaging",
                columns: table => new
                {
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProtectedContent = table.Column<string>(type: "nvarchar(max)", maxLength: 16384, nullable: false),
                    HasManualName = table.Column<bool>(type: "bit", nullable: false),
                    LinkedAccountUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServerVersion = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressBookEntries", x => new { x.OwnerUserId, x.Id });
                    table.CheckConstraint("CK_AddressBookEntries_Tombstone", "[DeletedAtUtc] IS NULL OR ([ProtectedContent] = N'' AND [LinkedAccountUserId] IS NULL)");
                    table.CheckConstraint("CK_AddressBookEntries_Version", "[ServerVersion] > 0");
                    table.ForeignKey(
                        name: "FK_AddressBookEntries_Users_LinkedAccountUserId",
                        column: x => x.LinkedAccountUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AddressBookEntries_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactSyncDevices",
                schema: "messaging",
                columns: table => new
                {
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsentVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConsentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SyncEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastSourceRevision = table.Column<long>(type: "bigint", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactSyncDevices", x => new { x.OwnerUserId, x.DeviceId });
                    table.CheckConstraint("CK_ContactSyncDevices_Revision", "[LastSourceRevision] >= 0");
                    table.ForeignKey(
                        name: "FK_ContactSyncDevices_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactSyncStates",
                schema: "messaging",
                columns: table => new
                {
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServerVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactSyncStates", x => x.OwnerUserId);
                    table.CheckConstraint("CK_ContactSyncStates_Version", "[ServerVersion] >= 0");
                    table.ForeignKey(
                        name: "FK_ContactSyncStates_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactPhones",
                schema: "messaging",
                columns: table => new
                {
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneIndex = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPhones", x => new { x.OwnerUserId, x.EntryId, x.PhoneIndex });
                    table.ForeignKey(
                        name: "FK_ContactPhones_AddressBookEntries_OwnerUserId_EntryId",
                        columns: x => new { x.OwnerUserId, x.EntryId },
                        principalSchema: "messaging",
                        principalTable: "AddressBookEntries",
                        principalColumns: new[] { "OwnerUserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactDeviceBindings",
                schema: "messaging",
                columns: table => new
                {
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRevision = table.Column<long>(type: "bigint", nullable: false),
                    ServerVersion = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactDeviceBindings", x => new { x.OwnerUserId, x.DeviceId, x.EntryId });
                    table.CheckConstraint("CK_ContactDeviceBindings_Versions", "[SourceRevision] > 0 AND [ServerVersion] > 0");
                    table.ForeignKey(
                        name: "FK_ContactDeviceBindings_AddressBookEntries_OwnerUserId_EntryId",
                        columns: x => new { x.OwnerUserId, x.EntryId },
                        principalSchema: "messaging",
                        principalTable: "AddressBookEntries",
                        principalColumns: new[] { "OwnerUserId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContactDeviceBindings_ContactSyncDevices_OwnerUserId_DeviceId",
                        columns: x => new { x.OwnerUserId, x.DeviceId },
                        principalSchema: "messaging",
                        principalTable: "ContactSyncDevices",
                        principalColumns: new[] { "OwnerUserId", "DeviceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactSyncOperations",
                schema: "messaging",
                columns: table => new
                {
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Receipt = table.Column<string>(type: "nvarchar(max)", maxLength: 65536, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactSyncOperations", x => new { x.OwnerUserId, x.DeviceId, x.ClientOperationId });
                    table.ForeignKey(
                        name: "FK_ContactSyncOperations_ContactSyncDevices_OwnerUserId_DeviceId",
                        columns: x => new { x.OwnerUserId, x.DeviceId },
                        principalSchema: "messaging",
                        principalTable: "ContactSyncDevices",
                        principalColumns: new[] { "OwnerUserId", "DeviceId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressBookEntries_LinkedAccountUserId",
                schema: "messaging",
                table: "AddressBookEntries",
                column: "LinkedAccountUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressBookEntries_OwnerUserId_ServerVersion",
                schema: "messaging",
                table: "AddressBookEntries",
                columns: new[] { "OwnerUserId", "ServerVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactDeviceBindings_OwnerUserId_EntryId",
                schema: "messaging",
                table: "ContactDeviceBindings",
                columns: new[] { "OwnerUserId", "EntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactPhones_OwnerUserId_PhoneIndex",
                schema: "messaging",
                table: "ContactPhones",
                columns: new[] { "OwnerUserId", "PhoneIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactDeviceBindings",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ContactPhones",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ContactSyncOperations",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ContactSyncStates",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "AddressBookEntries",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "ContactSyncDevices",
                schema: "messaging");

            migrationBuilder.DropColumn(
                name: "Bio",
                schema: "identity",
                table: "Users");
        }
    }
}
