using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes;

/* The rules a space type name must pass, shared by the create and update commands. */
public class SpaceTypeNameValidator : ITransientDependency
{
    private readonly IRepository<SpaceType, Guid> _types;

    public SpaceTypeNameValidator(IRepository<SpaceType, Guid> types)
    {
        _types = types;
    }

    /* Returns the trimmed name. excludeId is the type being renamed, so it doesn't clash with itself. */
    public async Task<string> ValidateAsync(string? raw, Guid? excludeId)
    {
        var name = raw?.Trim() ?? "";
        if (name.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the space type a name.");
        var lower = name.ToLower();
        if (await _types.AnyAsync(t => t.Name.ToLower() == lower && t.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.SpaceTypeDuplicate, $"There is already a space type called \"{name}\".");
        return name;
    }

    public async Task<SpaceType> GetAsync(Guid id)
        => await _types.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.SpaceTypeNotFound, "No space type with that ID.");
}
