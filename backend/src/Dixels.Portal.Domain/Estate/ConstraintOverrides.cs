namespace Dixels.Portal.Estate;

/* The four rules a floor or space may override. Null means "use the parent's value". */
public record ConstraintOverrides(int? OpenHour, int? CloseHour, int? MinBookingMinutes, int? MaxBookingHours);
