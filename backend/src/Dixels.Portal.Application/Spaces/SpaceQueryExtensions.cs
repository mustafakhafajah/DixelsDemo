using System.Linq;
using Volo.Abp;

namespace Dixels.Portal.Spaces;

public static class SpaceQueryExtensions
{
    /* The admin registry filters. Kept apart from the service so the same query can be checked
     * against both database providers in tests. */
    public static IQueryable<Space> ApplyRegistryFilter(this IQueryable<Space> query, GetSpacesInput input)
    {
        var name = input.Name?.Trim().ToLower();
        return query
            .WhereIf(input.BuildingId.HasValue, s => s.BuildingId == input.BuildingId)
            .WhereIf(input.FloorId.HasValue, s => s.FloorId == input.FloorId)
            /* A name typed in any language matches. */
            .WhereIf(!string.IsNullOrEmpty(name), s => s.Translations.Any(t => t.Name.ToLower().Contains(name!)))
            .WhereIf(input.TypeId.HasValue, s => s.TypeId == input.TypeId);
    }

    /* The picker list's filters: one building and / or one floor. */
    public static IQueryable<Space> ApplyPickerFilter(this IQueryable<Space> query, GetSpaceListInput input)
        => query
            .WhereIf(input.BuildingId.HasValue, s => s.BuildingId == input.BuildingId)
            .WhereIf(input.FloorId.HasValue, s => s.FloorId == input.FloorId);
}
