using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dixels.Portal.Bookings;

/* The database itself only refuses two bookings on one space at once (Add_Booking_No_Overlap_Constraint).
 * "One person, one space at a time" and "not in blocked time" are checked by the app, so two requests at the
 * same moment could both pass. These locks make such requests wait their turn: take them before the checks,
 * and they are held until the current transaction ends. Order: the owner first, then spaces. */
public interface IBookingLocks
{
    /* Everything that books for, or moves a booking of, this person. */
    Task LockOwnerAsync(Guid ownerUserId);

    /* Booking these spaces, blocking time on them, or deleting them. */
    Task LockSpacesAsync(IEnumerable<Guid> spaceIds);
}
