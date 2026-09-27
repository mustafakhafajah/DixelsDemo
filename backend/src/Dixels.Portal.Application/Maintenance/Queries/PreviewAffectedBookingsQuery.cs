using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Cqrs;

namespace Dixels.Portal.Maintenance.Queries;

/* Before blocking time, show the admin how many bookings each window would hit. Changes nothing. */
public record PreviewAffectedBookingsQuery(PreviewMaintenanceDto Input) : IQuery<AffectedBookingsPreviewDto>;

public class PreviewAffectedBookingsQueryHandler : IQueryHandler<PreviewAffectedBookingsQuery, AffectedBookingsPreviewDto>
{
    private readonly MaintenanceScopeResolver _scopes;
    private readonly MaintenanceBookingImpact _impact;

    public PreviewAffectedBookingsQueryHandler(MaintenanceScopeResolver scopes, MaintenanceBookingImpact impact)
    {
        _scopes = scopes;
        _impact = impact;
    }

    public async Task<AffectedBookingsPreviewDto> HandleAsync(PreviewAffectedBookingsQuery query)
    {
        var input = query.Input;
        var spaceIds = await _scopes.GetSpaceIdsAsync(input.ScopeType, input.ScopeId);
        var result = new AffectedBookingsPreviewDto { SpaceCount = spaceIds.Count };
        foreach (var o in input.Occurrences)
        {
            var (s, e) = (o.StartUtc.AsUtc(), o.EndUtc.AsUtc());
            var n = await _impact.CountAffectedAsync(spaceIds, s, e);
            result.PerOccurrence.Add(new OccurrenceAffectedCountDto { StartUtc = s, EndUtc = e, AffectedCount = n });
            result.TotalAffected += n;
        }
        return result;
    }
}
