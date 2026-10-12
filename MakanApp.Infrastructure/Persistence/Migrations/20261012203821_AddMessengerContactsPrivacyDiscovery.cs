using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessengerContactsPrivacyDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessagingPrivacySettings",
                schema: "messaging",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DirectMessagesAudience = table.Column<int>(type: "int", nullable: false),
                    PhoneNumberVisibilityAudience = table.Column<int>(type: "int", nullable: false),
                    PhoneNumberDiscoveryAudience = table.Column<int>(type: "int", nullable: false),
                    GroupInvitesAudience = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessagingPrivacySettings", x => x.UserId);
                    table.CheckConstraint("CK_MessagingPrivacySettings_DirectMessagesAudience", "[DirectMessagesAudience] IN (1, 2, 3)");
                    table.CheckConstraint("CK_MessagingPrivacySettings_GroupInvitesAudience", "[GroupInvitesAudience] IN (1, 2, 3)");
                    table.CheckConstraint("CK_MessagingPrivacySettings_PhoneNumberDiscoveryAudience", "[PhoneNumberDiscoveryAudience] IN (1, 2, 3)");
                    table.CheckConstraint("CK_MessagingPrivacySettings_PhoneNumberVisibilityAudience", "[PhoneNumberVisibilityAudience] IN (1, 2, 3)");
                    table.ForeignKey(
                        name: "FK_MessagingPrivacySettings_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserContacts",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SavedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContacts", x => x.Id);
                    table.CheckConstraint("CK_UserContacts_DistinctUsers", "[OwnerUserId] <> [ContactUserId]");
                    table.ForeignKey(
                        name: "FK_UserContacts_Users_ContactUserId",
                        column: x => x.ContactUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserContacts_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserContacts_ContactUserId",
                schema: "messaging",
                table: "UserContacts",
                column: "ContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserContacts_Owner_IsFavorite",
                schema: "messaging",
                table: "UserContacts",
                columns: new[] { "OwnerUserId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_UserContacts_Owner_UpdatedAtUtc",
                schema: "messaging",
                table: "UserContacts",
                columns: new[] { "OwnerUserId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_UserContacts_Owner_Contact",
                schema: "messaging",
                table: "UserContacts",
                columns: new[] { "OwnerUserId", "ContactUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessagingPrivacySettings",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "UserContacts",
                schema: "messaging");
        }
    }
}
