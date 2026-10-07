using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* Outside guests answer Accept / Maybe / Decline through a private link in their invitation. Only the SHA-256 of
     * that link's secret is stored (like a password), and it is wiped with the guest's address once the booking is
     * over. Guests invited before this change simply have none (their invitation went without buttons). */
    public partial class Guest_Response_Links : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResponseTokenHash",
                table: "AppBookingAttendees",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_ResponseTokenHash",
                table: "AppBookingAttendees",
                column: "ResponseTokenHash",
                unique: true,
                filter: "\"ResponseTokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppBookingAttendees_ResponseTokenHash",
                table: "AppBookingAttendees");

            migrationBuilder.DropColumn(
                name: "ResponseTokenHash",
                table: "AppBookingAttendees");
        }
    }
}
