using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dixels.Portal.Floors.Commands;

public record CreateFloorCommand(CreateUpdateFloorDto Input) : ICommand<FloorDto>;

public class CreateFloorCommandHandler : ICommandHandler<CreateFloorCommand, FloorDto>
{
    private readonly IRepository<Floor, Guid> _floors;
    private readonly FloorInputValidator _validator;
    private readonly FloorDtoMapper _mapper;
    private readonly IGuidGenerator _guids;

    public CreateFloorCommandHandler(IRepository<Floor, Guid> floors, FloorInputValidator validator,
        FloorDtoMapper mapper, IGuidGenerator guids)
    {
        _floors = floors;
        _validator = validator;
        _mapper = mapper;
        _guids = guids;
    }

    public async Task<FloorDto> HandleAsync(CreateFloorCommand command)
    {
        var building = await _validator.GetBuildingAsync(command.Input.BuildingId);
        var name = await _validator.ValidateAsync(building, command.Input, null);
        var floor = new Floor(_guids.Create(), building.Id, name);
        FloorInputValidator.Apply(floor, command.Input);
        await _floors.InsertAsync(floor, autoSave: true);
        return await _mapper.MapAsync(floor);
    }
}
