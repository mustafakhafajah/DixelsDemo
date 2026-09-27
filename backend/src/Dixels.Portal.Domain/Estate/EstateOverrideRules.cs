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
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject} cannot open earlier ({openOv}) than {parent} ({bounds.OpenHour}).");
        if (closeOv.HasValue && closeOv > bounds.CloseHour)
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject} cannot close later ({closeOv}) than {parent} ({bounds.CloseHour}).");
        var effOpen = openOv ?? bounds.OpenHour;
        var effClose = closeOv ?? bounds.CloseHour;
        if (effClose <= effOpen)
            throw new BusinessException(PortalDomainErrorCodes.InvalidHours, "Close hour must be after open hour.");
        if (minOv.HasValue && minOv < bounds.MinBookingMinutes)
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject}'s minimum duration cannot be less than {parentPossessive} ({bounds.MinBookingMinutes}m).");
        if (maxOv.HasValue && maxOv > bounds.MaxBookingHours)
            throw new BusinessException(PortalDomainErrorCodes.NarrowingViolation,
                $"{subject}'s maximum duration cannot exceed {parentPossessive} ({bounds.MaxBookingHours}h).");
    }
}
