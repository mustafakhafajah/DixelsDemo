using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* People invited to a booking: portal users (UserId) or outside guests by email (UserId null). A guest's email
     * is wiped once the booking is over or cancelled (BookingGuestCleaner), so both columns may be null and the row
     * only counts. Nothing existing changes: bookings without attendees work as before. */
    public partial class Add_Booking_Attendees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppBookingAttendees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBookingAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBookingAttendees_AppBookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "AppBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_BookingId_Email",
                table: "AppBookingAttendees",
                columns: new[] { "BookingId", "Email" },
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_BookingId_UserId",
                table: "AppBookingAttendees",
                columns: new[] { "BookingId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppBookingAttendees_UserId",
                table: "AppBookingAttendees",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBookingAttendees");
        }
    }
}
