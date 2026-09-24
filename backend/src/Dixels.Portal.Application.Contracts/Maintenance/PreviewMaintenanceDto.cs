using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Estate;

namespace Dixels.Portal.Maintenance;

public class PreviewMaintenanceDto
{
    public MaintenanceScopeType ScopeType { get; set; }
    [Required] public Guid ScopeId { get; set; }
    [Required, MinLength(1), MaxLength(EstateConsts.MaxRecurrenceOccurrences)]
    public List<TimeWindowDto> Occurrences { get; set; } = new();
}
