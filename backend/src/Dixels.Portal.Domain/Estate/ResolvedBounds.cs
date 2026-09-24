namespace Dixels.Portal.Estate;

/* What a child (floor or space) inherits and may only narrow. */
public record ResolvedBounds(int OpenHour, int CloseHour, int MinBookingMinutes, int MaxBookingHours);
