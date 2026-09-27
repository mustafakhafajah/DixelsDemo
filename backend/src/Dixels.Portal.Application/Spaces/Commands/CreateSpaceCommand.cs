using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dixels.Portal.Spaces.Commands;

public record CreateSpaceCommand(CreateUpdateSpaceDto Input) : ICommand<SpaceDto>;

public class CreateSpaceCommandHandler : ICommandHandler<CreateSpaceCommand, SpaceDto>
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly SpaceInputValidator _validator;
    private readonly SpaceDtoMapper _mapper;
    private readonly IGuidGenerator _guids;

    public CreateSpaceCommandHandler(IRepository<Space, Guid> spaces, SpaceInputValidator validator,
        SpaceDtoMapper mapper, IGuidGenerator guids)
    {
        _spaces = spaces;
        _validator = validator;
        _mapper = mapper;
        _guids = guids;
    }

    public async Task<SpaceDto> HandleAsync(CreateSpaceCommand command)
    {
        var (name, building, floor) = await _validator.ValidateAsync(command.Input, null);
        var space = new Space(_guids.Create(), name, building.Id, floor.Id, command.Input.TypeId);
        SpaceInputApplier.Apply(space, command.Input);
        await _spaces.InsertAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
    }
}
