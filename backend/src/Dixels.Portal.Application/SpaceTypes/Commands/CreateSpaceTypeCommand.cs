using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Dixels.Portal.SpaceTypes.Commands;

public record CreateSpaceTypeCommand(CreateUpdateSpaceTypeDto Input) : ICommand<SpaceTypeDto>;

public class CreateSpaceTypeCommandHandler : ICommandHandler<CreateSpaceTypeCommand, SpaceTypeDto>
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly SpaceTypeNameValidator _validator;
    private readonly IGuidGenerator _guids;

    public CreateSpaceTypeCommandHandler(IRepository<SpaceType, Guid> types, SpaceTypeNameValidator validator, IGuidGenerator guids)
    {
        _types = types;
        _validator = validator;
        _guids = guids;
    }

    public async Task<SpaceTypeDto> HandleAsync(CreateSpaceTypeCommand command)
    {
        var name = await _validator.ValidateAsync(command.Input.Name, null);
        var type = await _types.InsertAsync(new SpaceType(_guids.Create(), name), autoSave: true);
        return SpaceTypeDtoMapper.Map(type, 0);
    }
}
