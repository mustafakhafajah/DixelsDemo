using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dixels.Portal.Migrations
{
    /* No two confirmed bookings on one space may overlap - enforced by PostgreSQL itself, so two people
     * booking the same slot at the same moment can't both win (the app's "is it free?" check and the
     * insert are two steps; this closes the gap between them). Cancelled and deleted bookings don't
     * count, and touching ends ([10:00,11:00) and [11:00,12:00)) don't overlap.
     * BookingOverlap.Translate turns the violation (SQLSTATE 23P01) into the normal booking.conflict.
     * Raw SQL only: the EF model is unchanged. */
    public partial class Add_Booking_No_Overlap_Constraint : Migration
    {
        public const string ConstraintName = "EX_AppBookings_Space_NoOverlap";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            /* Stop with a clear message rather than a cryptic one if existing data already overlaps. */
            migrationBuilder.Sql(@"
DO $$
DECLARE clashes integer;
BEGIN
    SELECT count(*) INTO clashes
    FROM ""AppBookings"" a
    JOIN ""AppBookings"" b ON a.""SpaceId"" = b.""SpaceId"" AND a.""Id"" < b.""Id""
    WHERE a.""Status"" = 0 AND b.""Status"" = 0 AND NOT a.""IsDeleted"" AND NOT b.""IsDeleted""
      AND a.""StartUtc"" < b.""EndUtc"" AND b.""StartUtc"" < a.""EndUtc"";
    IF clashes > 0 THEN
        RAISE EXCEPTION 'Cannot add the no-overlap rule: % pair(s) of confirmed bookings already overlap on the same space. Cancel one of each pair first.', clashes;
    END IF;
END $$;");

            migrationBuilder.Sql($@"
ALTER TABLE ""AppBookings"" ADD CONSTRAINT ""{ConstraintName}""
    EXCLUDE USING gist (""SpaceId"" WITH =, tstzrange(""StartUtc"", ""EndUtc"", '[)') WITH &&)
    WHERE (""Status"" = 0 AND NOT ""IsDeleted"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"ALTER TABLE ""AppBookings"" DROP CONSTRAINT IF EXISTS ""{ConstraintName}"";");
        }
    }
}
