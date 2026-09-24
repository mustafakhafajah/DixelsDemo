using System.Linq;
using Volo.Abp;

namespace Dixels.Portal.Spaces;

public static class SpaceQueryExtensions
{
    private const string CodePrefix = "SP-";

    /* The admin registry filters. Kept apart from the service so the same query can be checked
     * against both database providers in tests. */
    public static IQueryable<Space> ApplyRegistryFilter(this IQueryable<Space> query, GetSpacesInput input)
    {
        var code = NormalizeCode(input.Code);
        var name = input.Name?.Trim().ToLower();
        return query
            .WhereIf(code != null, s => s.Id.ToString().ToLower().StartsWith(code!))
            .WhereIf(input.BuildingId.HasValue, s => s.BuildingId == input.BuildingId)
            .WhereIf(input.FloorId.HasValue, s => s.FloorId == input.FloorId)
            .WhereIf(!string.IsNullOrEmpty(name), s => s.Name.ToLower().Contains(name!))
            .WhereIf(input.TypeId.HasValue, s => s.TypeId == input.TypeId);
    }

    /* "SP-3F2A1B9C", "sp-3f2a", "3f2a1b9c-..." all become a lower-case GUID prefix. */
    internal static string? NormalizeCode(string? code)
    {
        var c = code?.Trim();
        if (string.IsNullOrEmpty(c)) return null;
        if (c.StartsWith(CodePrefix, System.StringComparison.OrdinalIgnoreCase)) c = c[CodePrefix.Length..];
        c = c.Trim().ToLowerInvariant();
        return c.Length == 0 ? null : c;
    }
}
