using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings.Commands;

public record SetBuildingBookableCommand(Guid Id, bool IsBookable) : ICommand<BuildingDto>;

public class SetBuildingBookableCommandHandler : ICommandHandler<SetBuildingBookableCommand, BuildingDto>
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly BuildingDtoMapper _mapper;

    public SetBuildingBookableCommandHandler(IRepository<Building, Guid> buildings, BuildingDtoMapper mapper)
    {
        _buildings = buildings;
        _mapper = mapper;
    }

    public async Task<BuildingDto> HandleAsync(SetBuildingBookableCommand command)
    {
        var building = await _buildings.GetAsync(command.Id);
        building.IsBookable = command.IsBookable;
        await _buildings.UpdateAsync(building, autoSave: true);
        return await _mapper.MapAsync(building);
    }
}
