using System;
using System.Threading.Tasks;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace Dixels.Portal.SpaceTypes;

/* Space type rules: a unique name, and a type in use can't be deleted. */
public class SpaceTypeManager : DomainService
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Space, Guid> _spaces;

    public SpaceTypeManager(IRepository<SpaceType, Guid> types, IRepository<Space, Guid> spaces)
    {
        _types = types;
        _spaces = spaces;
    }

    public async Task<SpaceType> CreateAsync(string name)
        => new SpaceType(GuidGenerator.Create(), await CheckNameAsync(name, null));

    public async Task ChangeNameAsync(SpaceType type, string name)
    {
        type.Name = await CheckNameAsync(name, type.Id);
    }

    public async Task EnsureCanDeleteAsync(SpaceType type)
    {
        var used = await _spaces.CountAsync(s => s.TypeId == type.Id);
        if (used > 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceTypeInUse, message:
                $"{used} space{(used == 1 ? " uses" : "s use")} \"{type.Name}\". Give them another type first.");
    }

    /* Returns the trimmed name. excludeId is the type being renamed, so it doesn't clash with itself. */
    private async Task<string> CheckNameAsync(string? name, Guid? excludeId)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: "Give the space type a name.").ForField("name");
        var lower = trimmed.ToLower();
        if (await _types.AnyAsync(t => t.Name.ToLower() == lower && t.Id != excludeId))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceTypeDuplicate, message: $"There is already a space type called \"{trimmed}\".").ForField("name");
        return trimmed;
    }
}
