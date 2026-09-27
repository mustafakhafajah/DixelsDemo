using System;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Cqrs;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dixels.Portal.Maintenance.Commands;

/* Block time on a space, a floor or a whole building: one row per space per window. */
public record ScheduleMaintenanceCommand(ScheduleMaintenanceDto Input) : ICommand<ScheduleMaintenanceResultDto>;

public class ScheduleMaintenanceCommandHandler : ICommandHandler<ScheduleMaintenanceCommand, ScheduleMaintenanceResultDto>
{
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly MaintenanceScopeResolver _scopes;
    private readonly MaintenanceBookingImpact _impact;
    private readonly IGuidGenerator _guids;

    public ScheduleMaintenanceCommandHandler(IRepository<MaintenanceWindow, Guid> maintenance, MaintenanceScopeResolver scopes,
        MaintenanceBookingImpact impact, IGuidGenerator guids)
    {
        _maintenance = maintenance;
        _scopes = scopes;
        _impact = impact;
        _guids = guids;
    }

    public async Task<ScheduleMaintenanceResultDto> HandleAsync(ScheduleMaintenanceCommand command)
    {
        var input = command.Input;
        var spaceIds = await _scopes.GetSpaceIdsAsync(input.ScopeType, input.ScopeId);
        foreach (var o in input.Occurrences)
        {
            if (o.StartUtc == default || o.EndUtc == default)
                throw new BusinessException(PortalDomainErrorCodes.MissingField, "Start and end are both required.");
            if (o.EndUtc <= o.StartUtc)
                throw new BusinessException(PortalDomainErrorCodes.EndBeforeStart, "End must be after start.");
        }

        var note = string.IsNullOrWhiteSpace(input.Note) ? "Blocked" : input.Note.Trim();
        var seriesId = spaceIds.Count * input.Occurrences.Count > 1 ? _guids.Create() : (Guid?)null;
        var created = 0;
        var affected = 0;
        var cancelled = 0;
        foreach (var o in input.Occurrences)
        {
            var (s, e) = (o.StartUtc.AsUtc(), o.EndUtc.AsUtc());
            affected += await _impact.CountAffectedAsync(spaceIds, s, e);
            if (input.CancelAffectedBookings) cancelled += await _impact.CancelAffectedAsync(spaceIds, s, e);
            foreach (var spaceId in spaceIds)
            {
                var m = new MaintenanceWindow(_guids.Create(), spaceId, s, e)
                {
                    Note = note,
                    SeriesId = seriesId,
                    ScopeType = input.ScopeType,
                    ScopeId = input.ScopeId,
                };
                await _maintenance.InsertAsync(m, autoSave: true);
                created++;
            }
        }

        return new ScheduleMaintenanceResultDto
        {
            SeriesId = seriesId, Created = created, AffectedBookingsCount = affected, CancelledBookingsCount = cancelled,
        };
    }
}
