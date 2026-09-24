using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Maintenance;

[Authorize]
public class MaintenanceWindowAppService : EstateAppServiceBase, IMaintenanceWindowAppService
{
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly MaintenanceScopeResolver _scopes;
    private readonly MaintenanceWindowDtoMapper _mapper;

    public MaintenanceWindowAppService(IRepository<MaintenanceWindow, Guid> maintenance,
        IRepository<Booking, Guid> bookings, MaintenanceScopeResolver scopes, MaintenanceWindowDtoMapper mapper)
    {
        _maintenance = maintenance;
        _bookings = bookings;
        _scopes = scopes;
        _mapper = mapper;
    }

    public async Task<ListResultDto<MaintenanceWindowDto>> GetListAsync(MaintenanceListFilterDto input)
    {
        var query = await _maintenance.GetQueryableAsync();
        if (!input.IncludeCancelled) query = query.Where(m => m.Status == MaintenanceStatus.Active);
        if (input.SpaceId.HasValue) query = query.Where(m => m.SpaceId == input.SpaceId.Value);
        if (input.FromUtc.HasValue) query = query.Where(m => m.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(m => m.StartUtc < input.ToUtc.Value);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(m => m.StartUtc));
        return new ListResultDto<MaintenanceWindowDto>(await _mapper.MapListAsync(list));
    }

    public async Task<MaintenanceWindowDto> GetAsync(Guid id) => await _mapper.MapAsync(await GetWindowAsync(id));

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public async Task<AffectedBookingsPreviewDto> PreviewAffectedBookingsAsync(PreviewMaintenanceDto input)
    {
        var spaceIds = await _scopes.GetSpaceIdsAsync(input.ScopeType, input.ScopeId);
        var result = new AffectedBookingsPreviewDto { SpaceCount = spaceIds.Count };
        foreach (var o in input.Occurrences)
        {
            var (s, e) = (o.StartUtc.AsUtc(), o.EndUtc.AsUtc());
            var n = await CountAffectedAsync(spaceIds, s, e);
            result.PerOccurrence.Add(new OccurrenceAffectedCountDto { StartUtc = s, EndUtc = e, AffectedCount = n });
            result.TotalAffected += n;
        }
        return result;
    }

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public async Task<ScheduleMaintenanceResultDto> ScheduleAsync(ScheduleMaintenanceDto input)
    {
        var spaceIds = await _scopes.GetSpaceIdsAsync(input.ScopeType, input.ScopeId);
        foreach (var o in input.Occurrences)
        {
            if (o.StartUtc == default || o.EndUtc == default)
                throw new BusinessException(PortalDomainErrorCodes.MissingField, "Start and end are both required.");
            if (o.EndUtc <= o.StartUtc)
                throw new BusinessException(PortalDomainErrorCodes.EndBeforeStart, "End must be after start.");
        }

        var note = string.IsNullOrWhiteSpace(input.Note) ? "Blocked" : input.Note.Trim();
        var seriesId = spaceIds.Count * input.Occurrences.Count > 1 ? GuidGenerator.Create() : (Guid?)null;
        var created = 0;
        var affected = 0;
        var cancelled = 0;
        foreach (var o in input.Occurrences)
        {
            var (s, e) = (o.StartUtc.AsUtc(), o.EndUtc.AsUtc());
            affected += await CountAffectedAsync(spaceIds, s, e);
            if (input.CancelAffectedBookings) cancelled += await CancelAffectedAsync(spaceIds, s, e);
            foreach (var spaceId in spaceIds)
            {
                var m = new MaintenanceWindow(GuidGenerator.Create(), spaceId, s, e)
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

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public async Task<MaintenanceWindowDto> CancelAsync(Guid id)
    {
        var m = await GetWindowAsync(id);
        if (m.Status != MaintenanceStatus.Cancelled)
        {
            m.Status = MaintenanceStatus.Cancelled;
            await _maintenance.UpdateAsync(m, autoSave: true);
        }
        return await _mapper.MapAsync(m);
    }

    private async Task<MaintenanceWindow> GetWindowAsync(Guid id)
        => await _maintenance.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.MaintenanceNotFound, "No blocked time with that ID.");

    /* Only bookings that have not started: a meeting already in progress is never cut off. */
    private async Task<int> CancelAffectedAsync(List<Guid> spaceIds, DateTime s, DateTime e)
    {
        var now = Clock.Now;
        var hit = await _bookings.GetListAsync(new OverlappingBookingsSpecification(s, e)
            .ToExpression().And(b => spaceIds.Contains(b.SpaceId) && b.StartUtc > now));
        foreach (var b in hit) b.Cancel();
        await _bookings.UpdateManyAsync(hit, autoSave: true);
        return hit.Count;
    }

    private async Task<int> CountAffectedAsync(List<Guid> spaceIds, DateTime s, DateTime e)
        => await _bookings.CountAsync(new OverlappingBookingsSpecification(s, e)
            .ToExpression().And(b => spaceIds.Contains(b.SpaceId)));
}
