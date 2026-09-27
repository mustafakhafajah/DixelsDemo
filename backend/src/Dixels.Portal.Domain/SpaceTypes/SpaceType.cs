using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Dixels.Portal.SpaceTypes;

/* A kind of space ("Meeting room", "Desk", ...). Admins manage the list; every space has exactly one type. */
public class SpaceType : AuditedAggregateRoot<Guid>
{
    public string Name { get; set; } = null!;

    protected SpaceType() { }

    public SpaceType(Guid id, string name) : base(id)
    {
        Name = name;
    }
}
