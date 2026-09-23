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
public class SpaceAppService : EstateAppServiceBase, ISpaceAppService
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Team, Guid> _teams;
    private readonly BookingManager _bookingManager;
    private readonly ActivityLogAppender _log;

    public SpaceAppService(IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings, IRepository<Team, Guid> teams,
        BookingManager bookingManager, ActivityLogAppender log)
    {
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _teams = teams;
        _bookingManager = bookingManager;
        _log = log;
    }

    public async Task<ListResultDto<SpaceDto>> GetListAsync()
    {
        var mapper = await CreateMapperAsync();
        var spaces = await _spaces.GetListAsync();
        return new ListResultDto<SpaceDto>(spaces
            .OrderBy(s => mapper.BuildingName(s)).ThenBy(s => s.Name)
            .Select(mapper.Map).ToList());
    }

    public async Task<SpaceDto> GetAsync(Guid id)
    {
        var space = await _spaces.FindAsync(id)
            ?? throw new BusinessException(PortalDomainErrorCodes.SpaceNotFound, "No space with that ID.");
        return (await CreateMapperAsync()).Map(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input)
    {
        var (name, building, floor) = await ValidateAsync(input, null);
        var space = new Space(GuidGenerator.Create(), name, building.Id, floor.Id);
        Apply(space, input, building);
        await _spaces.InsertAsync(space, autoSave: true);
        var mapper = await CreateMapperAsync();
        await _log.LogAsync(ActivityActions.SpaceCreated, ActivityEntityTypes.Space, space.Id,
            $"{name} · {building.Name} floor {floor.Name} · {mapper.TeamsLabel(space)}");
        return mapper.Map(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> UpdateAsync(Guid id, CreateUpdateSpaceDto input)
    {
        var space = await _spaces.GetAsync(id);
        var (name, building, floor) = await ValidateAsync(input, id);
        space.Name = name;
        space.BuildingId = building.Id;
        space.FloorId = floor.Id;
        Apply(space, input, building);
        await _spaces.UpdateAsync(space, autoSave: true);
        var mapper = await CreateMapperAsync();
        await _log.LogAsync(ActivityActions.SpaceUpdated, ActivityEntityTypes.Space, space.Id,
            $"{name} · {building.Name} floor {floor.Name} · {mapper.TeamsLabel(space)} · {space.Status.ToString().ToLower()}");
        return mapper.Map(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> SetStatusAsync(Guid id, SetStatusDto input)
    {
        var space = await _spaces.GetAsync(id);
        space.Status = input.Status;
        await _spaces.UpdateAsync(space, autoSave: true);
        await _log.LogAsync(ActivityActions.SpaceStatusChanged, ActivityEntityTypes.Space, space.Id,
            $"{space.Name} set to {space.Status.ToString().ToLower()}");
        return (await CreateMapperAsync()).Map(space);
    }

    private async Task<(string Name, Building Building, Floor Floor)> ValidateAsync(CreateUpdateSpaceDto input, Guid? excludeId)
    {
        var name = input.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the space a name.");
        var building = await _buildings.FindAsync(input.BuildingId)
            ?? throw new BusinessException(PortalDomainErrorCodes.MissingField, "Pick a building. Add one first if the list is empty.");
        var floor = await _floors.FindAsync(input.FloorId);
        if (floor == null || floor.BuildingId != building.Id)
            throw new BusinessException(PortalDomainErrorCodes.InvalidFloor, $"Pick a floor that belongs to {building.Name}.");
        var lower = name.ToLower();
        if (await _spaces.AnyAsync(s => s.Name.ToLower() == lower && s.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.SpaceDuplicateName, "Another space already uses that name.");

        EstateValidation.ValidateOverrides("Space", "its floor", "its floor's",
            ConstraintResolver.ResolveBounds(building, floor),
            input.OpenHourOverride, input.CloseHourOverride, input.MinBookingMinutesOverride, input.MaxBookingHoursOverride);
        return (name, building, floor);
    }

    private static void Apply(Space s, CreateUpdateSpaceDto input, Building building)
    {
        s.Type = input.Type;
        s.Status = input.Status;
        s.Capacity = Math.Max(0, input.Capacity);
        s.TimeZone = string.IsNullOrWhiteSpace(input.TimeZone) ? building.TimeZone : input.TimeZone.Trim();
        s.Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        s.RestrictedTeamIds = input.RestrictedTeamIds.Distinct().ToList();
        s.OpenHourOverride = input.OpenHourOverride;
        s.CloseHourOverride = input.CloseHourOverride;
        s.MinBookingMinutesOverride = input.MinBookingMinutesOverride;
        s.MaxBookingHoursOverride = input.MaxBookingHoursOverride;
    }

    private async Task<SpaceMapper> CreateMapperAsync()
    {
        var buildings = (await _buildings.GetListAsync()).ToDictionary(b => b.Id);
        var floors = (await _floors.GetListAsync()).ToDictionary(f => f.Id);
        var teams = (await _teams.GetListAsync()).ToDictionary(t => t.Id, t => t.Name);
        var isAdmin = await IsAdminAsync();
        var teamId = CurrentUser.Id.HasValue ? await _bookingManager.GetUserTeamIdAsync(CurrentUser.Id.Value) : null;
        return new SpaceMapper(buildings, floors, teams, isAdmin, teamId);
    }

    private sealed class SpaceMapper
    {
        private readonly Dictionary<Guid, Building> _buildings;
        private readonly Dictionary<Guid, Floor> _floors;
        private readonly Dictionary<Guid, string> _teams;
        private readonly bool _isAdmin;
        private readonly Guid? _teamId;

        public SpaceMapper(Dictionary<Guid, Building> buildings, Dictionary<Guid, Floor> floors,
            Dictionary<Guid, string> teams, bool isAdmin, Guid? teamId)
        {
            _buildings = buildings;
            _floors = floors;
            _teams = teams;
            _isAdmin = isAdmin;
            _teamId = teamId;
        }

        public string BuildingName(Space s) => _buildings.TryGetValue(s.BuildingId, out var b) ? b.Name : "";

        public string TeamsLabel(Space s) => s.RestrictedTeamIds.Count == 0
            ? "open to all teams"
            : string.Join(", ", s.RestrictedTeamIds.Select(id => _teams.GetValueOrDefault(id, "?"))) + " only";

        public SpaceDto Map(Space s)
        {
            var building = _buildings[s.BuildingId];
            _floors.TryGetValue(s.FloorId, out var floor);
            var ctx = new SpaceContext(s, floor, building);
            var c = ctx.Constraints;
            return new SpaceDto
            {
                Id = s.Id,
                Name = s.Name,
                Type = s.Type,
                Status = s.Status,
                BuildingId = s.BuildingId,
                BuildingName = building.Name,
                FloorId = s.FloorId,
                FloorName = floor?.Name ?? "",
                TimeZone = s.TimeZone,
                Capacity = s.Capacity,
                RestrictedTeamIds = s.RestrictedTeamIds.ToList(),
                RestrictedTeamNames = s.RestrictedTeamIds.Select(id => _teams.GetValueOrDefault(id, "?")).ToList(),
                Note = s.Note,
                OpenHourOverride = s.OpenHourOverride,
                CloseHourOverride = s.CloseHourOverride,
                MinBookingMinutesOverride = s.MinBookingMinutesOverride,
                MaxBookingHoursOverride = s.MaxBookingHoursOverride,
                Constraints = new ResolvedConstraintsDto
                {
                    OpenMinute = c.OpenMinute,
                    CloseMinute = c.CloseMinute,
                    MinBookingMinutes = c.MinBookingMinutes,
                    MaxBookingHours = c.MaxBookingHours,
                    Holidays = c.Holidays.ToList(),
                },
                CanCurrentUserBook = BookingManager.CanBook(ctx, _teamId, _isAdmin),
            };
        }
    }
}
