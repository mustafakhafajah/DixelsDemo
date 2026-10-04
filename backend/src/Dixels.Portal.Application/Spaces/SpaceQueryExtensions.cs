using System;
using System.Collections.Generic;
using System.Linq;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Volo.Abp;

namespace Dixels.Portal.Spaces;

public static class SpaceQueryExtensions
{
    /* The space collection's filters. Kept apart from the service so the same query can be checked
     * against both database providers in tests. */
    public static IQueryable<Space> ApplyListFilter(this IQueryable<Space> query, GetSpaceListInput input)
    {
        var name = input.Name?.Trim().ToLower();
        var minCapacity = input.MinCapacity ?? 0;
        var typeIds = input.TypeIds ?? new List<Guid>();
        return query
            .WhereIf(input.BuildingId.HasValue, s => s.BuildingId == input.BuildingId)
            .WhereIf(input.FloorId.HasValue, s => s.FloorId == input.FloorId)
            /* A name typed in any language matches, so people find a space by whichever name they know. */
            .WhereIf(!string.IsNullOrEmpty(name), s => s.Translations.Any(t => t.Name.ToLower().Contains(name!)))
            .WhereIf(typeIds.Count > 0, s => typeIds.Contains(s.TypeId))
            .WhereIf(minCapacity > 0, s => s.Capacity >= minCapacity);
    }

    /* Bookable = the building, the floor and the space are all bookable (same rule as BookingManager.CanBook;
     * a space whose floor no longer exists is judged by its building and itself). null keeps every space. */
    public static IQueryable<Space> ApplyBookableFilter(this IQueryable<Space> spaces, bool? bookable,
        IQueryable<Building> buildings, IQueryable<Floor> floors)
    {
        if (!bookable.HasValue) return spaces;
        var wanted = bookable.Value;
        return from s in spaces
               join b in buildings on s.BuildingId equals b.Id
               join f in floors on s.FloorId equals f.Id into floorJoin
               from f in floorJoin.DefaultIfEmpty()
               where (s.IsBookable && b.IsBookable && (f == null || f.IsBookable)) == wanted
               select s;
    }
}
