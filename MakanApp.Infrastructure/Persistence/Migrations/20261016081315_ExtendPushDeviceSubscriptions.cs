using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MakanApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendPushDeviceSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebPushAuth",
                schema: "notification",
                table: "PushDeviceRegistrations",
                type: "varchar(22)",
                unicode: false,
                maxLength: 22,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebPushP256dh",
                schema: "notification",
                table: "PushDeviceRegistrations",
                type: "varchar(87)",
                unicode: false,
                maxLength: 87,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PushDeviceRegistrations_WebKeys",
                schema: "notification",
                table: "PushDeviceRegistrations",
                sql: "([WebPushP256dh] IS NULL AND [WebPushAuth] IS NULL) OR ([Platform] = 3 AND [WebPushP256dh] IS NOT NULL AND LEN([WebPushP256dh]) = 87 AND [WebPushAuth] IS NOT NULL AND LEN([WebPushAuth]) = 22)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PushDeviceRegistrations_WebKeys",
                schema: "notification",
                table: "PushDeviceRegistrations");

            migrationBuilder.DropColumn(
                name: "WebPushAuth",
                schema: "notification",
                table: "PushDeviceRegistrations");

            migrationBuilder.DropColumn(
                name: "WebPushP256dh",
                schema: "notification",
                table: "PushDeviceRegistrations");
        }
    }
}
