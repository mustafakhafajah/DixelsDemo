using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes;

/* Space types are part of managing spaces, so they use the same permission. */
[Authorize]
public class SpaceTypeAppService : EstateAppServiceBase, ISpaceTypeAppService
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Space, Guid> _spaces;

    public SpaceTypeAppService(IRepository<SpaceType, Guid> types, IRepository<Space, Guid> spaces)
    {
        _types = types;
        _spaces = spaces;
    }

    public async Task<ListResultDto<SpaceTypeDto>> GetListAsync()
    {
        var types = await _types.GetListAsync();
        var counts = (await _spaces.GetListAsync()).GroupBy(s => s.TypeId).ToDictionary(g => g.Key, g => g.Count());
        return new ListResultDto<SpaceTypeDto>(types.OrderBy(t => t.Name)
            .Select(t => Map(t, counts.GetValueOrDefault(t.Id))).ToList());
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceTypeDto> CreateAsync(CreateUpdateSpaceTypeDto input)
    {
        var name = await ValidateNameAsync(input.Name, null);
        var type = await _types.InsertAsync(new SpaceType(GuidGenerator.Create(), name), autoSave: true);
        return Map(type, 0);
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task<SpaceTypeDto> UpdateAsync(Guid id, CreateUpdateSpaceTypeDto input)
    {
        var type = await GetTypeAsync(id);
        type.Name = await ValidateNameAsync(input.Name, id);
        await _types.UpdateAsync(type, autoSave: true);
        return Map(type, await _spaces.CountAsync(s => s.TypeId == id));
    }

    [Authorize(PortalPermissions.Spaces.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        var type = await GetTypeAsync(id);
        var used = await _spaces.CountAsync(s => s.TypeId == id);
        if (used > 0)
            throw new BusinessException(PortalDomainErrorCodes.SpaceTypeInUse,
                $"{used} space{(used == 1 ? " uses" : "s use")} \"{type.Name}\". Give them another type first.");
        await _types.DeleteAsync(type, autoSave: true);
    }

    private async Task<SpaceType> GetTypeAsync(Guid id)
        => await _types.FindAsync(id)
           ?? throw new BusinessException(PortalDomainErrorCodes.SpaceTypeNotFound, "No space type with that ID.");

    private async Task<string> ValidateNameAsync(string? raw, Guid? excludeId)
    {
        var name = raw?.Trim() ?? "";
        if (name.Length == 0)
            throw new BusinessException(PortalDomainErrorCodes.MissingField, "Give the space type a name.");
        var lower = name.ToLower();
        if (await _types.AnyAsync(t => t.Name.ToLower() == lower && t.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.SpaceTypeDuplicate, $"There is already a space type called \"{name}\".");
        return name;
    }

    private static SpaceTypeDto Map(SpaceType t, int spaceCount) => new() { Id = t.Id, Name = t.Name, SpaceCount = spaceCount };
}
