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
    public const string UnknownTimeZone = "validation.time_zone_unknown";
    public const string InvalidWeekday = "validation.invalid_weekday";
    public const string UnsupportedLanguage = "validation.unsupported_language";
    public const string TooLong = "validation.too_long";
    public const string WrongScript = "validation.wrong_script";
    public const string InvalidPhone = "validation.invalid_phone";
    public const string PictureTooLarge = "validation.picture_too_large";
    public const string PictureNotImage = "validation.picture_not_image";
    /* ABP Identity refused a profile change or a password (taken user name, wrong current password, password rules). */
    public const string IdentityRejected = "validation.identity_rejected";

    public const string SpaceNotFound = "space.not_found";
    public const string SpaceNotBookable = "space.not_bookable";
    public const string SpaceDuplicateName = "space.duplicate_name";
    public const string SpaceUnderMaintenance = "space.under_maintenance";
    public const string BuildingNotBookable = "building.not_bookable";
    public const string BuildingDuplicate = "building.duplicate";
    public const string FloorNotBookable = "floor.not_bookable";
    public const string FloorDuplicate = "floor.duplicate";
    public const string SpaceTypeNotFound = "space_type.not_found";
    public const string SpaceTypeDuplicate = "space_type.duplicate";
    public const string SpaceTypeInUse = "space_type.in_use";
    /* Deleting something that still has floors, spaces or bookings that haven't ended under it. */
    public const string BuildingNotEmpty = "building.not_empty";
    public const string FloorNotEmpty = "floor.not_empty";
    public const string SpaceHasBookings = "space.has_bookings";

    public const string AccessForbidden = "access.forbidden";

    public const string BookingNotFound = "booking.not_found";
    public const string BookingConflict = "booking.conflict";
    public const string BookingSelfOverlap = "booking.self_overlap";
    public const string BookingLocked = "booking.locked";
    public const string BookingInProgress = "booking.in_progress";
    public const string BookingNotInProgress = "booking.not_in_progress";
    /* The owner plus the people invited don't fit the room. */
    public const string BookingOverCapacity = "booking.over_capacity";
    /* Someone who can't be invited: an unknown or inactive user, the owner, a bad email address, or listed twice. */
    public const string BookingInvalidAttendee = "booking.invalid_attendee";
    /* Leaving a booking you aren't invited to. */
    public const string BookingNotAttendee = "booking.not_attendee";
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
        [UnknownTimeZone] = HttpStatusCode.BadRequest,
        [InvalidWeekday] = HttpStatusCode.BadRequest,
        [UnsupportedLanguage] = HttpStatusCode.BadRequest,
        [TooLong] = HttpStatusCode.BadRequest,
        [WrongScript] = HttpStatusCode.BadRequest,
        [InvalidPhone] = HttpStatusCode.BadRequest,
        [PictureTooLarge] = HttpStatusCode.BadRequest,
        [PictureNotImage] = HttpStatusCode.BadRequest,
        [IdentityRejected] = HttpStatusCode.BadRequest,
        [SpaceNotFound] = HttpStatusCode.NotFound,
        [SpaceNotBookable] = HttpStatusCode.Conflict,
        [SpaceDuplicateName] = HttpStatusCode.Conflict,
        [SpaceUnderMaintenance] = HttpStatusCode.Conflict,
        [BuildingNotBookable] = HttpStatusCode.Conflict,
        [BuildingDuplicate] = HttpStatusCode.Conflict,
        [FloorNotBookable] = HttpStatusCode.Conflict,
        [FloorDuplicate] = HttpStatusCode.Conflict,
        [SpaceTypeNotFound] = HttpStatusCode.NotFound,
        [SpaceTypeDuplicate] = HttpStatusCode.Conflict,
        [SpaceTypeInUse] = HttpStatusCode.Conflict,
        [BuildingNotEmpty] = HttpStatusCode.Conflict,
        [FloorNotEmpty] = HttpStatusCode.Conflict,
        [SpaceHasBookings] = HttpStatusCode.Conflict,
        [AccessForbidden] = HttpStatusCode.Forbidden,
        [BookingNotFound] = HttpStatusCode.NotFound,
        [BookingConflict] = HttpStatusCode.Conflict,
        [BookingSelfOverlap] = HttpStatusCode.Conflict,
        [BookingLocked] = HttpStatusCode.Conflict,
        [BookingInProgress] = HttpStatusCode.Conflict,
        [BookingNotInProgress] = HttpStatusCode.Conflict,
        [BookingOverCapacity] = HttpStatusCode.BadRequest,
        [BookingInvalidAttendee] = HttpStatusCode.BadRequest,
        [BookingNotAttendee] = HttpStatusCode.Conflict,
        [VersionMismatch] = HttpStatusCode.Conflict,
        [MaintenanceNotFound] = HttpStatusCode.NotFound,
    };
}
