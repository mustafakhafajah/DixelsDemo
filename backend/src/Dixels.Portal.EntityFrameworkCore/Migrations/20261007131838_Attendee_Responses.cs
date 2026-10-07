using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* Invited people can now answer, as in Teams: Response (0 none yet, 1 accepted, 2 tentative, 3 declined) and when
     * they last answered. Everyone already invited starts at 0, "not answered yet", so nothing existing changes. */
    public partial class Attendee_Responses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "AppBookingAttendees",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Response",
                table: "AppBookingAttendees",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "AppBookingAttendees");

            migrationBuilder.DropColumn(
                name: "Response",
                table: "AppBookingAttendees");
        }
    }
}
