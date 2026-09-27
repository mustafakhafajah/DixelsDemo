using System;

namespace Dixels.Portal.Spaces;

/* Copies a valid space form onto the entity. Checking the form is the validator's job; this only copies,
 * so create and update fill the space the same way. */
public static class SpaceInputApplier
{
    public static void Apply(Space s, CreateUpdateSpaceDto input)
    {
        s.TypeId = input.TypeId;
        s.IsBookable = input.IsBookable;
        s.Capacity = Math.Max(0, input.Capacity);
        s.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        s.OpenHourOverride = input.OpenHourOverride;
        s.CloseHourOverride = input.CloseHourOverride;
        s.MinBookingMinutesOverride = input.MinBookingMinutesOverride;
        s.MaxBookingHoursOverride = input.MaxBookingHoursOverride;
    }
}
