using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Buildings.Commands;

public record UpdateBuildingCommand(Guid Id, CreateUpdateBuildingDto Input) : ICommand<BuildingDto>;

public class UpdateBuildingCommandHandler : ICommandHandler<UpdateBuildingCommand, BuildingDto>
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly BuildingInputValidator _validator;
    private readonly BuildingDtoMapper _mapper;

    public UpdateBuildingCommandHandler(IRepository<Building, Guid> buildings, BuildingInputValidator validator,
        BuildingDtoMapper mapper)
    {
        _buildings = buildings;
        _validator = validator;
        _mapper = mapper;
    }

    public async Task<BuildingDto> HandleAsync(UpdateBuildingCommand command)
    {
        var building = await _buildings.GetAsync(command.Id);
        building.Name = await _validator.ValidateAsync(command.Input, command.Id);
        BuildingInputApplier.Apply(building, command.Input);
        await _buildings.UpdateAsync(building, autoSave: true);
        return await _mapper.MapAsync(building);
    }
}
