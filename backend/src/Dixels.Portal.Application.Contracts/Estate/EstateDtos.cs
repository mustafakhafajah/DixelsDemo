using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Estate;

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
    public List<TimeWindowDto> Occurrences { get; set; } = new();
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
