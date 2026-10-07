namespace Dixels.Portal.Bookings;

public static class BookingConsts
{
    public const int MaxIdempotencyKeyLength = 128;
    /* Same limit as ABP's user email. */
    public const int MaxAttendeeEmailLength = 256;
    public const int MaxAttendeeNameLength = 128;
    /* People invited to one booking; the room's capacity usually stops it much earlier. */
    public const int MaxAttendees = 100;
}
