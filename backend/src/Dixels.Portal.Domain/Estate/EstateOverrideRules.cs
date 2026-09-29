using Volo.Abp;

namespace Dixels.Portal.Estate;

/* A floor or space may only narrow the rules it inherits, never widen them (mock: submitFloor / submitSpace).
 * This is a business rule, so it lives in the Domain, next to ConstraintResolver that produces the bounds. */
public static class EstateOverrideRules
{
    public static void EnsureOnlyNarrows(string subject, string parent, string parentPossessive, ResolvedBounds bounds,
        int? openOv, int? closeOv, int? minOv, int? maxOv)
    {
        if (openOv.HasValue && openOv < bounds.OpenHour)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                $"{subject} cannot open earlier ({openOv}) than {parent} ({bounds.OpenHour}).").ForField("openHourOverride");
        if (closeOv.HasValue && closeOv > bounds.CloseHour)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                $"{subject} cannot close later ({closeOv}) than {parent} ({bounds.CloseHour}).").ForField("closeHourOverride");
        var effOpen = openOv ?? bounds.OpenHour;
        var effClose = closeOv ?? bounds.CloseHour;
        if (effClose <= effOpen)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidHours, message: "Close hour must be after open hour.")
                .ForField(closeOv.HasValue || !openOv.HasValue ? "closeHourOverride" : "openHourOverride");
        if (minOv.HasValue && minOv < bounds.MinBookingMinutes)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                $"{subject}'s minimum duration cannot be less than {parentPossessive} ({bounds.MinBookingMinutes}m).").ForField("minBookingMinutesOverride");
        if (maxOv.HasValue && maxOv > bounds.MaxBookingHours)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                $"{subject}'s maximum duration cannot exceed {parentPossessive} ({bounds.MaxBookingHours}h).").ForField("maxBookingHoursOverride");
    }
}
