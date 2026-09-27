using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces.Commands;

public record UpdateSpaceCommand(Guid Id, CreateUpdateSpaceDto Input) : ICommand<SpaceDto>;

public class UpdateSpaceCommandHandler : ICommandHandler<UpdateSpaceCommand, SpaceDto>
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly SpaceInputValidator _validator;
    private readonly SpaceDtoMapper _mapper;

    public UpdateSpaceCommandHandler(IRepository<Space, Guid> spaces, SpaceInputValidator validator, SpaceDtoMapper mapper)
    {
        _spaces = spaces;
        _validator = validator;
        _mapper = mapper;
    }

    public async Task<SpaceDto> HandleAsync(UpdateSpaceCommand command)
    {
        var space = await _spaces.GetAsync(command.Id);
        var (name, building, floor) = await _validator.ValidateAsync(command.Input, command.Id);
        space.Name = name;
        space.BuildingId = building.Id;
        space.FloorId = floor.Id;
        SpaceInputValidator.Apply(space, command.Input);
        await _spaces.UpdateAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
    }
}
