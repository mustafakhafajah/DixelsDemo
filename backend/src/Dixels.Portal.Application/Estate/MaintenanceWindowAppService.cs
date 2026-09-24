using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Estate;

[Authorize]
public class MaintenanceWindowAppService : EstateAppServiceBase, IMaintenanceWindowAppService
{
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;

    public MaintenanceWindowAppService(IRepository<MaintenanceWindow, Guid> maintenance,
        IRepository<Booking, Guid> bookings, IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings)
    {
        _maintenance = maintenance;
        _bookings = bookings;
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
    }

    public async Task<ListResultDto<MaintenanceWindowDto>> GetListAsync(MaintenanceListFilterDto input)
    {
        var query = await _maintenance.GetQueryableAsync();
        if (!input.IncludeCancelled) query = query.Where(m => m.Status == MaintenanceStatus.Active);
        if (input.SpaceId.HasValue) query = query.Where(m => m.SpaceId == input.SpaceId.Value);
        if (input.FromUtc.HasValue) query = query.Where(m => m.EndUtc > input.FromUtc.Value);
        if (input.ToUtc.HasValue) query = query.Where(m => m.StartUtc < input.ToUtc.Value);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(m => m.StartUtc));
        return new ListResultDto<MaintenanceWindowDto>(await MapManyAsync(list));
    }

    public async Task<MaintenanceWindowDto> GetAsync(Guid id)
        => (await MapManyAsync(new() { await GetWindowAsync(id) }))[0];

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public async Task<AffectedBookingsPreviewDto> PreviewAffectedBookingsAsync(PreviewMaintenanceDto input)
    {
        var spaceIds = await ResolveScopeAsync(input.ScopeType, input.ScopeId);
        var result = new AffectedBookingsPreviewDto { SpaceCount = spaceIds.Count };
        foreach (var o in input.Occurrences)
        {
            var (s, e) = (Utc(o.StartUtc), Utc(o.EndUtc));
            var n = await CountAffectedAsync(spaceIds, s, e);
            result.PerOccurrence.Add(new OccurrenceAffectedCountDto { StartUtc = s, EndUtc = e, AffectedCount = n });
            result.TotalAffected += n;
        }
        return result;
    }

    [Authorize(PortalPermissions.Maintenance.Manage)]
    public async Task<ScheduleMaintenanceResultDto> ScheduleAsync(ScheduleMaintenanceDto input)
    {
        var spaceIds = await ResolveScopeAsync(input.ScopeType, input.ScopeId);
        foreach (var o in input.Occurrences)
        {
            if (o.StartUtc == default || o.EndUtc == default)
                throw new BusinessException(PortalDomainErrorCodes.MissingField, "Start and end are both required.");
            if (o.EndUtc <= o.StartUtc)
                throw new BusinessException(PortalDomainErrorCodes.EndBeforeStart, "End must be after start.");
        }

        var note = string.IsNullOrWhiteSpace(input.Note) ? "Cleaning" : input.Note.Trim();
        var seriesId = spaceIds.Count * input.Occurrences.Count > 1 ? GuidGenerator.Create() : (Guid?)null;
        var created = 0;
        var affected = 0;
        foreach (var o in input.Occurrences)
        {
            var (s, e) = (Utc(o.StartUtc), Utc(o.EndUtc));
            affected += await CountAffectedAsync(spaceIds, s, e);
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

        return new ScheduleMaintenanceResultDto { SeriesId = seriesId, Created = created, AffectedBookingsCount = affected };
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
        return (await MapManyAsync(new() { m }))[0];
    }

    private async Task<MaintenanceWindow> GetWindowAsync(Guid id)
        => await _maintenance.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.MaintenanceNotFound, "No cleaning entry with that ID.");

    private async Task<List<Guid>> ResolveScopeAsync(MaintenanceScopeType type, Guid scopeId)
    {
        var ids = type switch
        {
            MaintenanceScopeType.Space => await _spaces.AnyAsync(s => s.Id == scopeId) ? new List<Guid> { scopeId } : new(),
            MaintenanceScopeType.Floor => (await _spaces.GetListAsync(s => s.FloorId == scopeId)).Select(s => s.Id).ToList(),
            _ => (await _spaces.GetListAsync(s => s.BuildingId == scopeId)).Select(s => s.Id).ToList(),
        };
        if (ids.Count == 0)
            throw new BusinessException(PortalDomainErrorCodes.SpaceNotFound, "That scope has no spaces to clean.");
        return ids;
    }

    private async Task<int> CountAffectedAsync(List<Guid> spaceIds, DateTime s, DateTime e)
        => await _bookings.CountAsync(b => spaceIds.Contains(b.SpaceId) && b.Status == BookingStatus.Confirmed &&
                                            b.StartUtc < e && s < b.EndUtc);

    private async Task<string> ScopeLabelAsync(MaintenanceScopeType type, Guid scopeId)
    {
        switch (type)
        {
            case MaintenanceScopeType.Space:
                return (await _spaces.FindAsync(scopeId))?.Name ?? "a space";
            case MaintenanceScopeType.Floor:
                var f = await _floors.FindAsync(scopeId);
                if (f == null) return "a floor";
                var fb = await _buildings.FindAsync(f.BuildingId);
                return $"{fb?.Name} · Floor {f.Name}";
            default:
                return (await _buildings.FindAsync(scopeId))?.Name ?? "a building";
        }
    }

    private static DateTime Utc(DateTime d) => d.Kind == DateTimeKind.Utc ? d
        : d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : DateTime.SpecifyKind(d, DateTimeKind.Utc);

    private async Task<List<MaintenanceWindowDto>> MapManyAsync(List<MaintenanceWindow> list)
    {
        if (list.Count == 0) return new();
        var spaceIds = list.Select(m => m.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name);
        var labels = new Dictionary<(MaintenanceScopeType, Guid), string>();
        foreach (var key in list.Select(m => (m.ScopeType, m.ScopeId)).Distinct())
            labels[key] = await ScopeLabelAsync(key.ScopeType, key.ScopeId);
        var now = Clock.Now;
        return list.Select(m => new MaintenanceWindowDto
        {
            Id = m.Id,
            SpaceId = m.SpaceId,
            SpaceName = spaces.GetValueOrDefault(m.SpaceId, "Unknown space"),
            StartUtc = m.StartUtc,
            EndUtc = m.EndUtc,
            Note = m.Note,
            SeriesId = m.SeriesId,
            ScopeType = m.ScopeType,
            ScopeId = m.ScopeId,
            ScopeLabel = labels[(m.ScopeType, m.ScopeId)],
            Status = m.Status,
            Lifecycle = m.GetLifecycle(now),
            CreationTime = m.CreationTime,
            CreatorId = m.CreatorId,
        }).ToList();
    }
}
