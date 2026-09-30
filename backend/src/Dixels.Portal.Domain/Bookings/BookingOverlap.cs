using System;
using System.Data.Common;
using System.Threading.Tasks;
using Volo.Abp;

namespace Dixels.Portal.Bookings;

/* The database's own "no two confirmed bookings on one space may overlap" rule (migration
 * Add_Booking_No_Overlap_Constraint) is the last word when two people book the same slot at the same
 * moment: both pass the "is it free?" check, only one insert survives. This turns the loser's database
 * error into the same friendly booking.conflict everyone else gets. */
public static class BookingOverlap
{
    /* PostgreSQL's SQLSTATE for an exclusion-constraint violation. */
    public const string ExclusionViolation = "23P01";

    public static bool IsOverlap(Exception? ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is DbException db && db.SqlState == ExclusionViolation) return true;
        }
        return false;
    }

    /* Runs a save; if the database refuses it as an overlap, throws the normal conflict instead. */
    public static async Task<T> Translate<T>(Func<Task<T>> save)
    {
        try
        {
            return await save();
        }
        catch (Exception ex) when (IsOverlap(ex))
        {
            throw Conflict();
        }
    }

    public static Task Translate(Func<Task> save) => Translate(async () => { await save(); return true; });

    public static BookingRaceException Conflict() => new();
}

/* "Someone got there first": the same code and message as an ordinary clash. It is its own type so a
 * series stops here instead of skipping the date and carrying on - after a database error PostgreSQL
 * refuses the rest of the transaction, so the series is refused as a whole and simply retried. */
public class BookingRaceException : UserFriendlyException
{
    public BookingRaceException()
        : base("The space is already booked for part of that window. Someone booked it at the same moment - please try again.",
            PortalDomainErrorCodes.BookingConflict)
    {
        this.WithData(ErrorFieldExtensions.FieldKey, "window");
    }
}
