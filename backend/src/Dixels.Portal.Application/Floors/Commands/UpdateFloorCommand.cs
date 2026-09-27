using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors.Commands;

public record UpdateFloorCommand(Guid Id, CreateUpdateFloorDto Input) : ICommand<FloorDto>;

public class UpdateFloorCommandHandler : ICommandHandler<UpdateFloorCommand, FloorDto>
{
    private readonly IRepository<Floor, Guid> _floors;
    private readonly FloorInputValidator _validator;
    private readonly FloorDtoMapper _mapper;

    public UpdateFloorCommandHandler(IRepository<Floor, Guid> floors, FloorInputValidator validator, FloorDtoMapper mapper)
    {
        _floors = floors;
        _validator = validator;
        _mapper = mapper;
    }

    /* A floor stays in its building: the edit form can't move it, so the input's BuildingId is ignored here. */
    public async Task<FloorDto> HandleAsync(UpdateFloorCommand command)
    {
        var floor = await _floors.GetAsync(command.Id);
        var building = await _validator.GetBuildingAsync(floor.BuildingId);
        floor.Name = await _validator.ValidateAsync(building, command.Input, command.Id);
        FloorInputApplier.Apply(floor, command.Input);
        await _floors.UpdateAsync(floor, autoSave: true);
        return await _mapper.MapAsync(floor);
    }
}
