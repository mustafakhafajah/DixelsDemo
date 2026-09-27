namespace Dixels.Portal.SpaceTypes;

/* Turns a space type into SpaceTypeDto. The caller passes the usage count it already has. */
public static class SpaceTypeDtoMapper
{
    public static SpaceTypeDto Map(SpaceType t, int spaceCount) => new() { Id = t.Id, Name = t.Name, SpaceCount = spaceCount };
}
