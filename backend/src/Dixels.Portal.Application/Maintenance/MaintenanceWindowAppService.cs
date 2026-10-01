using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Maintenance;

/* Blocked time: Maintenance.Default to see it, Create to block time (and preview), Delete to unblock. */
[Authorize]
public class MaintenanceWindowAppService : PortalAppService, IMaintenanceWindowAppService
{
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly MaintenanceScopeResolver _scopes;

    public MaintenanceWindowAppService(IRepository<MaintenanceWindow, Guid> maintenance, IRepository<Booking, Guid> bookings,
        IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors, IRepository<Building, Guid> buildings,
        MaintenanceScopeResolver scopes)
    {
        _maintenance = maintenance;
        _bookings = bookings;
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _scopes = scopes;
    }

    [Authorize(PortalPermissions.Maintenance.Default)]
    public async Task<ListResultDto<MaintenanceWindowDto>> GetListAsync(MaintenanceListFilterDto input)
    {
        var query = await _maintenance.GetQueryableAsync();
        if (!input.IncludeCancelled) query = query.Where(m => m.Status == MaintenanceStatus.Active);
        if (input.SpaceId.HasValue) query = query.Where(m => m.SpaceId == input.SpaceId.Value);
        /* The Building / Floor filters: only windows on spaces of that building / floor (a subquery). */
        if (input.BuildingId.HasValue || input.FloorId.HasValue)
        {
            var spaces = await _spaces.GetQueryableAsync();
            if (input.BuildingId.HasValue) spaces = spaces.Where(s => s.BuildingId == input.BuildingId.Value);
            if (input.FloorId.HasValue) spaces = spaces.Where(s => s.FloorId == input.FloorId.Value);
            var spaceIds = spaces.Select(s => s.Id);
            query = query.Where(m => spaceIds.Contains(m.SpaceId));
        }
        if (input.FromUtc.HasValue) query = query.Where(m => m.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(m => m.StartUtc < input.ToUtc.Value);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(m => m.StartUtc));
        return new ListResultDto<MaintenanceWindowDto>(await MapListAsync(list));
    }

    [Authorize(PortalPermissions.Maintenance.Default)]
    public async Task<MaintenanceWindowDto> GetAsync(Guid id) => await MapAsync(await GetWindowAsync(id));

    /* Before blocking time, show the admin how many bookings each window would hit. Changes nothing. */
    [Authorize(PortalPermissions.Maintenance.Create)]
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

    /* Block time on a space, a floor or a whole building: one row per space per window. */
    [Authorize(PortalPermissions.Maintenance.Create)]
    public async Task<ScheduleMaintenanceResultDto> ScheduleAsync(ScheduleMaintenanceDto input)
    {
        var spaceIds = await _scopes.GetSpaceIdsAsync(input.ScopeType, input.ScopeId);
        foreach (var o in input.Occurrences)
        {
            if (o.StartUtc == default || o.EndUtc == default)
                throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:StartEndRequired"]).ForField("start");
            if (o.EndUtc <= o.StartUtc)
                throw new UserFriendlyException(code: PortalDomainErrorCodes.EndBeforeStart, message: L["Error:EndBeforeStart"]).ForField("end");
        }

        var note = string.IsNullOrWhiteSpace(input.Note) ? L["Blocked"] : input.Note.Trim();
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

    /* Unblock a window. Cancelling twice is harmless. */
    [Authorize(PortalPermissions.Maintenance.Delete)]
    public async Task<MaintenanceWindowDto> CancelAsync(Guid id)
    {
        var m = await GetWindowAsync(id);
        if (m.Status != MaintenanceStatus.Cancelled)
        {
            m.Status = MaintenanceStatus.Cancelled;
            await _maintenance.UpdateAsync(m, autoSave: true);
        }
        return await MapAsync(m);
    }

    private async Task<MaintenanceWindow> GetWindowAsync(Guid id)
        => await _maintenance.FindAsync(id)
           ?? throw new UserFriendlyException(code: PortalDomainErrorCodes.MaintenanceNotFound, message: L["Error:MaintenanceNotFound"]);

    /* The preview and the real schedule count "affected" the same way: confirmed bookings overlapping the window. */
    private async Task<int> CountAffectedAsync(List<Guid> spaceIds, DateTime startUtc, DateTime endUtc)
        => await _bookings.CountAsync(new OverlappingBookingsSpecification(startUtc, endUtc)
            .ToExpression().And(b => spaceIds.Contains(b.SpaceId)));

    /* Only bookings that have not started: a meeting already in progress is never cut off. */
    private async Task<int> CancelAffectedAsync(List<Guid> spaceIds, DateTime startUtc, DateTime endUtc)
    {
        var now = Clock.Now;
        var hit = await _bookings.GetListAsync(new OverlappingBookingsSpecification(startUtc, endUtc)
            .ToExpression().And(b => spaceIds.Contains(b.SpaceId) && b.StartUtc > now));
        foreach (var b in hit) b.Cancel();
        await _bookings.UpdateManyAsync(hit, autoSave: true);
        return hit.Count;
    }

    private async Task<MaintenanceWindowDto> MapAsync(MaintenanceWindow window)
        => (await MapListAsync(new List<MaintenanceWindow> { window }))[0];

    /* ObjectMapper copies the window; the space name and the label of the original scope ("HQ North · Floor 3")
     * are looked up once per distinct space and scope. */
    private async Task<List<MaintenanceWindowDto>> MapListAsync(List<MaintenanceWindow> list)
    {
        if (list.Count == 0) return new();
        var spaceIds = list.Select(m => m.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.GetName());
        var labels = new Dictionary<(MaintenanceScopeType, Guid), string>();
        foreach (var key in list.Select(m => (m.ScopeType, m.ScopeId)).Distinct())
            labels[key] = await ScopeLabelAsync(key.ScopeType, key.ScopeId);
        var now = Clock.Now;
        return list.Select(m =>
        {
            var dto = ObjectMapper.Map<MaintenanceWindow, MaintenanceWindowDto>(m);
            dto.SpaceName = spaces.GetValueOrDefault(m.SpaceId) ?? L["UnknownSpace"];
            dto.ScopeLabel = labels[(m.ScopeType, m.ScopeId)];
            dto.Lifecycle = m.GetLifecycle(now).ToApiValue();
            return dto;
        }).ToList();
    }

    private async Task<string> ScopeLabelAsync(MaintenanceScopeType type, Guid scopeId)
    {
        switch (type)
        {
            case MaintenanceScopeType.Space:
                return (await _spaces.FindAsync(scopeId))?.GetName() ?? L["ASpace"];
            case MaintenanceScopeType.Floor:
                var f = await _floors.FindAsync(scopeId);
                if (f == null) return L["AFloor"];
                var fb = await _buildings.FindAsync(f.BuildingId);
                return L["BuildingFloorLabel", fb?.GetName() ?? "", f.GetName()];
            default:
                return (await _buildings.FindAsync(scopeId))?.GetName() ?? L["ABuilding"];
        }
    }
}
