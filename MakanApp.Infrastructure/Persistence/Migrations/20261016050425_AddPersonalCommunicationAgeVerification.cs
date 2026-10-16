using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalCommunicationAgeVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CommunicationAgeVerifications_Source",
                schema: "identity",
                table: "CommunicationAgeVerifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CommunicationAgeVerifications_Source",
                schema: "identity",
                table: "CommunicationAgeVerifications",
                sql: "([Source]=1 AND [OrganizationId] IS NOT NULL) OR ([Source] IN (2,3) AND [OrganizationId] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CommunicationAgeVerifications_Source",
                schema: "identity",
                table: "CommunicationAgeVerifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CommunicationAgeVerifications_Source",
                schema: "identity",
                table: "CommunicationAgeVerifications",
                sql: "([Source]=1 AND [OrganizationId] IS NOT NULL) OR ([Source]=2 AND [OrganizationId] IS NULL)");
        }
    }
}
