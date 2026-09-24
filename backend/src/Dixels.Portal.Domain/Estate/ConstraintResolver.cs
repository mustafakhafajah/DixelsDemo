using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;

namespace Dixels.Portal.Estate;

/* Space overrides Floor overrides Building (mock: resolveConstraints / resolvedBounds). */
public static class ConstraintResolver
{
    public static ResolvedConstraints Resolve(Building building, Floor? floor, Space space)
    {
        return new ResolvedConstraints(
            Pick(space.OpenHourOverride, floor?.OpenHourOverride, building.OpenHour) * 60,
            Pick(space.CloseHourOverride, floor?.CloseHourOverride, building.CloseHour) * 60,
            Pick(space.MinBookingMinutesOverride, floor?.MinBookingMinutesOverride, building.MinBookingMinutes),
            Pick(space.MaxBookingHoursOverride, floor?.MaxBookingHoursOverride, building.MaxBookingHours),
            building.Holidays);
    }

    public static ResolvedBounds ResolveBounds(Building building, Floor? floor)
    {
        return new ResolvedBounds(
            floor?.OpenHourOverride ?? building.OpenHour,
            floor?.CloseHourOverride ?? building.CloseHour,
            floor?.MinBookingMinutesOverride ?? building.MinBookingMinutes,
            floor?.MaxBookingHoursOverride ?? building.MaxBookingHours);
    }

    private static int Pick(int? spaceValue, int? floorValue, int buildingValue)
        => spaceValue ?? floorValue ?? buildingValue;
}
