using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces;

[Authorize]
public class SpaceAppService : EstateAppServiceBase, ISpaceAppService
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly SpaceDtoMapper _mapper;

    public SpaceAppService(IRepository<Space, Guid> spaces, IRepository<Floor, Guid> floors,
        IRepository<Building, Guid> buildings, SpaceDtoMapper mapper)
    {
        _spaces = spaces;
        _floors = floors;
        _buildings = buildings;
        _mapper = mapper;
    }

    public async Task<ListResultDto<SpaceDto>> GetListAsync()
    {
        var dtos = await _mapper.MapListAsync(await _spaces.GetListAsync());
        return new ListResultDto<SpaceDto>(dtos.OrderBy(d => d.BuildingName).ThenBy(d => d.Name).ToList());
    }

    public async Task<SpaceDto> GetAsync(Guid id)
    {
        var space = await _spaces.FindAsync(id)
            ?? throw new BusinessException(PortalDomainErrorCodes.SpaceNotFound, "No space with that ID.");
        return await _mapper.MapAsync(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> CreateAsync(CreateUpdateSpaceDto input)
    {
        var (name, building, floor) = await ValidateAsync(input, null);
        var space = new Space(GuidGenerator.Create(), name, building.Id, floor.Id);
        Apply(space, input, building);
        await _spaces.InsertAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
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
        return await _mapper.MapAsync(space);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceDto> SetStatusAsync(Guid id, SetStatusDto input)
    {
        var space = await _spaces.GetAsync(id);
        space.Status = input.Status;
        await _spaces.UpdateAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
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

        EstateOverrideRules.EnsureOnlyNarrows("Space", "its floor", "its floor's",
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
        s.OpenHourOverride = input.OpenHourOverride;
        s.CloseHourOverride = input.CloseHourOverride;
        s.MinBookingMinutesOverride = input.MinBookingMinutesOverride;
        s.MaxBookingHoursOverride = input.MaxBookingHoursOverride;
    }
}
