using System.Collections.Generic;
using System.Net;

namespace Dixels.Portal;

/* Codes match the mock's API error codes; the SPA branches on them. */
public static class PortalDomainErrorCodes
{
    public const string MissingField = "validation.missing_field";
    public const string EndBeforeStart = "validation.end_before_start";
    public const string StartInPast = "validation.start_in_past";
    public const string HolidayClosed = "validation.holiday_closed";
    public const string OutsideHours = "validation.outside_hours";
    public const string DurationBelowMin = "validation.duration_below_min";
    public const string DurationAboveMax = "validation.duration_above_max";
    public const string InvalidHours = "validation.invalid_hours";
    public const string NarrowingViolation = "validation.narrowing_violation";
    public const string InvalidFloor = "validation.invalid_floor";
    public const string InvalidSpaceType = "validation.invalid_space_type";

    public const string SpaceNotFound = "space.not_found";
    public const string SpaceInactive = "space.inactive";
    public const string SpaceDuplicateName = "space.duplicate_name";
    public const string SpaceUnderMaintenance = "space.under_maintenance";
    public const string BuildingInactive = "building.inactive";
    public const string BuildingDuplicate = "building.duplicate";
    public const string FloorInactive = "floor.inactive";
    public const string FloorDuplicate = "floor.duplicate";
    public const string SpaceTypeNotFound = "space_type.not_found";
    public const string SpaceTypeDuplicate = "space_type.duplicate";
    public const string SpaceTypeInUse = "space_type.in_use";

    public const string AccessForbidden = "access.forbidden";

    public const string BookingNotFound = "booking.not_found";
    public const string BookingConflict = "booking.conflict";
    public const string BookingSelfOverlap = "booking.self_overlap";
    public const string BookingLocked = "booking.locked";
    public const string BookingInProgress = "booking.in_progress";
    public const string BookingNotInProgress = "booking.not_in_progress";
    public const string VersionMismatch = "conflict.version_mismatch";
    public const string MaintenanceNotFound = "maintenance.not_found";

    public static readonly IReadOnlyDictionary<string, HttpStatusCode> StatusCodes = new Dictionary<string, HttpStatusCode>
    {
        [MissingField] = HttpStatusCode.BadRequest,
        [EndBeforeStart] = HttpStatusCode.BadRequest,
        [StartInPast] = HttpStatusCode.BadRequest,
        [HolidayClosed] = HttpStatusCode.BadRequest,
        [OutsideHours] = HttpStatusCode.BadRequest,
        [DurationBelowMin] = HttpStatusCode.BadRequest,
        [DurationAboveMax] = HttpStatusCode.BadRequest,
        [InvalidHours] = HttpStatusCode.BadRequest,
        [NarrowingViolation] = HttpStatusCode.BadRequest,
        [InvalidFloor] = HttpStatusCode.BadRequest,
        [InvalidSpaceType] = HttpStatusCode.BadRequest,
        [SpaceNotFound] = HttpStatusCode.NotFound,
        [SpaceInactive] = HttpStatusCode.Conflict,
        [SpaceDuplicateName] = HttpStatusCode.Conflict,
        [SpaceUnderMaintenance] = HttpStatusCode.Conflict,
        [BuildingInactive] = HttpStatusCode.Conflict,
        [BuildingDuplicate] = HttpStatusCode.Conflict,
        [FloorInactive] = HttpStatusCode.Conflict,
        [FloorDuplicate] = HttpStatusCode.Conflict,
        [SpaceTypeNotFound] = HttpStatusCode.NotFound,
        [SpaceTypeDuplicate] = HttpStatusCode.Conflict,
        [SpaceTypeInUse] = HttpStatusCode.Conflict,
        [AccessForbidden] = HttpStatusCode.Forbidden,
        [BookingNotFound] = HttpStatusCode.NotFound,
        [BookingConflict] = HttpStatusCode.Conflict,
        [BookingSelfOverlap] = HttpStatusCode.Conflict,
        [BookingLocked] = HttpStatusCode.Conflict,
        [BookingInProgress] = HttpStatusCode.Conflict,
        [BookingNotInProgress] = HttpStatusCode.Conflict,
        [VersionMismatch] = HttpStatusCode.Conflict,
        [MaintenanceNotFound] = HttpStatusCode.NotFound,
    };
}
