using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* The parking option on bookings is gone, so its column goes too. */
    public partial class Remove_Booking_Parking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Parking",
                table: "AppBookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Parking",
                table: "AppBookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
