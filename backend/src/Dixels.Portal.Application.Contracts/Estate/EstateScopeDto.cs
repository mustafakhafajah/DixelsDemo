using System;
using System.ComponentModel.DataAnnotations;
using Dixels.Portal.Maintenance;

namespace Dixels.Portal.Estate;

/* A space, a whole floor or a whole building. */
public class EstateScopeDto
{
    public MaintenanceScopeType ScopeType { get; set; }
    [Required] public Guid ScopeId { get; set; }
}
