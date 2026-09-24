using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;

namespace Dixels.Portal.Spaces;

/* A space together with the floor and building it inherits its rules from. */
public record SpaceContext(Space Space, Floor? Floor, Building Building)
{
    public ResolvedConstraints Constraints => ConstraintResolver.Resolve(Building, Floor, Space);
}
