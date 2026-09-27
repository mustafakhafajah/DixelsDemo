using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes.Commands;

/* Only a type no space uses can be deleted. */
public record DeleteSpaceTypeCommand(Guid Id) : ICommand<Unit>;

public class DeleteSpaceTypeCommandHandler : ICommandHandler<DeleteSpaceTypeCommand, Unit>
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly SpaceTypeNameValidator _validator;

    public DeleteSpaceTypeCommandHandler(IRepository<SpaceType, Guid> types, IRepository<Space, Guid> spaces,
        SpaceTypeNameValidator validator)
    {
        _types = types;
        _spaces = spaces;
        _validator = validator;
    }

    public async Task<Unit> HandleAsync(DeleteSpaceTypeCommand command)
    {
        var type = await _validator.GetAsync(command.Id);
        var used = await _spaces.CountAsync(s => s.TypeId == command.Id);
        if (used > 0)
            throw new BusinessException(PortalDomainErrorCodes.SpaceTypeInUse,
                $"{used} space{(used == 1 ? " uses" : "s use")} \"{type.Name}\". Give them another type first.");
        await _types.DeleteAsync(type, autoSave: true);
        return Unit.Value;
    }
}
