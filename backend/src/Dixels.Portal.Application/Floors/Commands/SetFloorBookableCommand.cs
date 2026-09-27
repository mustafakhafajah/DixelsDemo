using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Floors.Commands;

public record SetFloorBookableCommand(Guid Id, bool IsBookable) : ICommand<FloorDto>;

public class SetFloorBookableCommandHandler : ICommandHandler<SetFloorBookableCommand, FloorDto>
{
    private readonly IRepository<Floor, Guid> _floors;
    private readonly FloorDtoMapper _mapper;

    public SetFloorBookableCommandHandler(IRepository<Floor, Guid> floors, FloorDtoMapper mapper)
    {
        _floors = floors;
        _mapper = mapper;
    }

    public async Task<FloorDto> HandleAsync(SetFloorBookableCommand command)
    {
        var floor = await _floors.GetAsync(command.Id);
        floor.IsBookable = command.IsBookable;
        await _floors.UpdateAsync(floor, autoSave: true);
        return await _mapper.MapAsync(floor);
    }
}
