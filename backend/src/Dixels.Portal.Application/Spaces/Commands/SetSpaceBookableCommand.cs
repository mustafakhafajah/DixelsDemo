using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Spaces.Commands;

public record SetSpaceBookableCommand(Guid Id, bool IsBookable) : ICommand<SpaceDto>;

public class SetSpaceBookableCommandHandler : ICommandHandler<SetSpaceBookableCommand, SpaceDto>
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly SpaceDtoMapper _mapper;

    public SetSpaceBookableCommandHandler(IRepository<Space, Guid> spaces, SpaceDtoMapper mapper)
    {
        _spaces = spaces;
        _mapper = mapper;
    }

    public async Task<SpaceDto> HandleAsync(SetSpaceBookableCommand command)
    {
        var space = await _spaces.GetAsync(command.Id);
        space.IsBookable = command.IsBookable;
        await _spaces.UpdateAsync(space, autoSave: true);
        return await _mapper.MapAsync(space);
    }
}
