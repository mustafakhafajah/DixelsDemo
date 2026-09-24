using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Estate;

public class ResolvedConstraintsDto
{
    public int OpenMinute { get; set; }
    public int CloseMinute { get; set; }
    public int MinBookingMinutes { get; set; }
    public int MaxBookingHours { get; set; }
    public List<DateOnly> Holidays { get; set; } = new();
}

public class SpaceDto : EntityDto<Guid>
{
    public string Name { get; set; } = null!;
    public SpaceType Type { get; set; }
    public EstateStatus Status { get; set; }
    public Guid BuildingId { get; set; }
    public string BuildingName { get; set; } = null!;
    public Guid FloorId { get; set; }
    public string FloorName { get; set; } = null!;
    public string TimeZone { get; set; } = null!;
    public int Capacity { get; set; }
    public string? Note { get; set; }
    public int? OpenHourOverride { get; set; }
    public int? CloseHourOverride { get; set; }
    public int? MinBookingMinutesOverride { get; set; }
    public int? MaxBookingHoursOverride { get; set; }
    public ResolvedConstraintsDto Constraints { get; set; } = new();
    public bool CanCurrentUserBook { get; set; }
}

public class CreateUpdateSpaceDto
{
    [Required, StringLength(EstateConsts.MaxNameLength)]
    public string Name { get; set; } = null!;
    public SpaceType Type { get; set; }
    public EstateStatus Status { get; set; }
    [Required] public Guid BuildingId { get; set; }
    [Required] public Guid FloorId { get; set; }
    [StringLength(EstateConsts.MaxTimeZoneLength)]
    public string? TimeZone { get; set; }
    [Range(0, 999)] public int Capacity { get; set; }
    [StringLength(EstateConsts.MaxNoteLength)]
    public string? Note { get; set; }
    [Range(0, 23)] public int? OpenHourOverride { get; set; }
    [Range(1, 24)] public int? CloseHourOverride { get; set; }
    [Range(5, 1440)] public int? MinBookingMinutesOverride { get; set; }
    [Range(1, 24)] public int? MaxBookingHoursOverride { get; set; }
}

public class SetStatusDto
{
    public EstateStatus Status { get; set; }
}

public class BookingDto : EntityDto<Guid>
{
    public Guid SpaceId { get; set; }
    public string SpaceName { get; set; } = null!;
    public Guid OwnerUserId { get; set; }
    public string OwnerName { get; set; } = null!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public BookingStatus Status { get; set; }
    public string Lifecycle { get; set; } = null!;
    public int Version { get; set; }
    public Guid? SeriesId { get; set; }
    public bool Parking { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

public class BookingListFilterDto
{
    public Guid? SpaceId { get; set; }
    public Guid? OwnerUserId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public bool IncludeCancelled { get; set; }
}

public class CreateBookingDto
{
    [Required] public Guid SpaceId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool Parking { get; set; }
    [StringLength(EstateConsts.MaxIdempotencyKeyLength)]
    public string? IdempotencyKey { get; set; }
}

public class BookingWindowDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}

public class CreateBookingSeriesDto
{
    [Required] public Guid SpaceId { get; set; }
    [Required, MinLength(1), MaxLength(EstateConsts.MaxRecurrenceOccurrences)]
    public List<BookingWindowDto> Occurrences { get; set; } = new();
    public bool Parking { get; set; }
}

public class BookingWindowFailureDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string ErrorCode { get; set; } = null!;
    public string ErrorMessage { get; set; } = null!;
}

public class CreateBookingSeriesResultDto
{
    public Guid? SeriesId { get; set; }
    public List<BookingDto> Created { get; set; } = new();
    public List<BookingWindowFailureDto> Skipped { get; set; } = new();
}

public class RescheduleBookingDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int? ExpectedVersion { get; set; }
}

public class CancelSeriesResultDto
{
    public Guid? SeriesId { get; set; }
    public int CancelledCount { get; set; }
}

public class MaintenanceWindowDto : EntityDto<Guid>
{
    public Guid SpaceId { get; set; }
    public string SpaceName { get; set; } = null!;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Note { get; set; }
    public Guid? SeriesId { get; set; }
    public MaintenanceScopeType ScopeType { get; set; }
    public Guid ScopeId { get; set; }
    public string ScopeLabel { get; set; } = null!;
    public MaintenanceStatus Status { get; set; }
    public string Lifecycle { get; set; } = null!;
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
}

public class MaintenanceListFilterDto
{
    public Guid? SpaceId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public bool IncludeCancelled { get; set; }
}

public class PreviewMaintenanceDto
{
    public MaintenanceScopeType ScopeType { get; set; }
    [Required] public Guid ScopeId { get; set; }
    [Required, MinLength(1), MaxLength(EstateConsts.MaxRecurrenceOccurrences)]
    public List<BookingWindowDto> Occurrences { get; set; } = new();
}

public class OccurrenceAffectedCountDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int AffectedCount { get; set; }
}

public class AffectedBookingsPreviewDto
{
    public int SpaceCount { get; set; }
    public int TotalAffected { get; set; }
    public List<OccurrenceAffectedCountDto> PerOccurrence { get; set; } = new();
}

public class ScheduleMaintenanceDto : PreviewMaintenanceDto
{
    [StringLength(EstateConsts.MaxNoteLength)]
    public string? Note { get; set; }
}

public class ScheduleMaintenanceResultDto
{
    public Guid? SeriesId { get; set; }
    public int Created { get; set; }
    public int AffectedBookingsCount { get; set; }
}

public class CurrentUserProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
}

public class UserLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsAdmin { get; set; }
}
