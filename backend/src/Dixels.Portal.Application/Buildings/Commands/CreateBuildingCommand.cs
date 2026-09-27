using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dixels.Portal.Buildings.Commands;

public record CreateBuildingCommand(CreateUpdateBuildingDto Input) : ICommand<BuildingDto>;

public class CreateBuildingCommandHandler : ICommandHandler<CreateBuildingCommand, BuildingDto>
{
    private readonly IRepository<Building, Guid> _buildings;
    private readonly BuildingInputValidator _validator;
    private readonly BuildingDtoMapper _mapper;
    private readonly IGuidGenerator _guids;

    public CreateBuildingCommandHandler(IRepository<Building, Guid> buildings, BuildingInputValidator validator,
        BuildingDtoMapper mapper, IGuidGenerator guids)
    {
        _buildings = buildings;
        _validator = validator;
        _mapper = mapper;
        _guids = guids;
    }

    public async Task<BuildingDto> HandleAsync(CreateBuildingCommand command)
    {
        var name = await _validator.ValidateAsync(command.Input, null);
        var building = new Building(_guids.Create(), name);
        BuildingInputApplier.Apply(building, command.Input);
        await _buildings.InsertAsync(building, autoSave: true);
        return await _mapper.MapAsync(building);
    }
}
