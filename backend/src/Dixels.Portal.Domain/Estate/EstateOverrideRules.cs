using Microsoft.Extensions.Localization;
using Volo.Abp;

namespace Dixels.Portal.Estate;

/* A floor or space may only narrow the rules it inherits, never widen them (mock: submitFloor / submitSpace).
 * This is a business rule, so it lives in the Domain, next to ConstraintResolver that produces the bounds.
 * level is "Floor" (inherits from the building) or "Space" (inherits from its floor); it picks the sentence. */
public static class EstateOverrideRules
{
    public const string FloorLevel = "Floor";
    public const string SpaceLevel = "Space";

    public static void EnsureOnlyNarrows(IStringLocalizer l, string level, ResolvedBounds bounds,
        int? openOv, int? closeOv, int? minOv, int? maxOv)
    {
        if (openOv.HasValue && openOv < bounds.OpenHour)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                l[$"Error:OpensEarlier:{level}", openOv, bounds.OpenHour]).ForField("openHourOverride");
        if (closeOv.HasValue && closeOv > bounds.CloseHour)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                l[$"Error:ClosesLater:{level}", closeOv, bounds.CloseHour]).ForField("closeHourOverride");
        var effOpen = openOv ?? bounds.OpenHour;
        var effClose = closeOv ?? bounds.CloseHour;
        if (effClose <= effOpen)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.InvalidHours, message: l["Error:CloseBeforeOpen"])
                .ForField(closeOv.HasValue || !openOv.HasValue ? "closeHourOverride" : "openHourOverride");
        if (minOv.HasValue && minOv < bounds.MinBookingMinutes)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                l[$"Error:MinBelowParent:{level}", bounds.MinBookingMinutes]).ForField("minBookingMinutesOverride");
        if (maxOv.HasValue && maxOv > bounds.MaxBookingHours)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.NarrowingViolation, message:
                l[$"Error:MaxAboveParent:{level}", bounds.MaxBookingHours]).ForField("maxBookingHoursOverride");
    }
}
