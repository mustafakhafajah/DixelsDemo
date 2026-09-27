using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Spaces;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes.Commands;

public record UpdateSpaceTypeCommand(Guid Id, CreateUpdateSpaceTypeDto Input) : ICommand<SpaceTypeDto>;

public class UpdateSpaceTypeCommandHandler : ICommandHandler<UpdateSpaceTypeCommand, SpaceTypeDto>
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly SpaceTypeNameValidator _validator;

    public UpdateSpaceTypeCommandHandler(IRepository<SpaceType, Guid> types, IRepository<Space, Guid> spaces,
        SpaceTypeNameValidator validator)
    {
        _types = types;
        _spaces = spaces;
        _validator = validator;
    }

    public async Task<SpaceTypeDto> HandleAsync(UpdateSpaceTypeCommand command)
    {
        var type = await _validator.GetAsync(command.Id);
        type.Name = await _validator.ValidateAsync(command.Input.Name, command.Id);
        await _types.UpdateAsync(type, autoSave: true);
        return SpaceTypeDtoMapper.Map(type, await _spaces.CountAsync(s => s.TypeId == command.Id));
    }
}
