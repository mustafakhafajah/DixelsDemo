using System.Linq;

namespace Dixels.Portal.Buildings;

/* Copies a valid building form onto the entity. Checking the form is the validator's job; this only copies,
 * so create and update fill the building the same way. */
public static class BuildingInputApplier
{
    public static void Apply(Building b, CreateUpdateBuildingDto input)
    {
        b.TimeZone = string.IsNullOrWhiteSpace(input.TimeZone) ? "UTC" : input.TimeZone.Trim();
        b.IsBookable = input.IsBookable;
        b.OpenHour = input.OpenHour;
        b.CloseHour = input.CloseHour;
        b.MinBookingMinutes = input.MinBookingMinutes;
        b.MaxBookingHours = input.MaxBookingHours;
        b.Holidays = input.Holidays.Distinct().OrderBy(d => d).ToList();
    }
}
