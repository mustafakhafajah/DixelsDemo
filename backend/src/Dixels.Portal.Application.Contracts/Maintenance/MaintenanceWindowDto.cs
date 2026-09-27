using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Maintenance;

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
