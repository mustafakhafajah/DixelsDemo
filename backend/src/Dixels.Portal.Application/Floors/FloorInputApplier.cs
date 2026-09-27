namespace Dixels.Portal.Floors;

/* Copies a valid floor form onto the entity. Checking the form is the validator's job; this only copies,
 * so create and update fill the floor the same way. */
public static class FloorInputApplier
{
    public static void Apply(Floor f, CreateUpdateFloorDto input)
    {
        f.IsBookable = input.IsBookable;
        f.OpenHourOverride = input.OpenHourOverride;
        f.CloseHourOverride = input.CloseHourOverride;
        f.MinBookingMinutesOverride = input.MinBookingMinutesOverride;
        f.MaxBookingHoursOverride = input.MaxBookingHoursOverride;
    }
}
