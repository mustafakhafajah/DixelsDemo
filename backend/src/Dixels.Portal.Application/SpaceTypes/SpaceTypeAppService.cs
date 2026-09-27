using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Localization;
using Dixels.Portal.Permissions;
using Dixels.Portal.Spaces;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes;

/* Reading is open to every signed-in user (the Find page filters by type); changes need the SpaceTypes permissions. */
[Authorize]
public class SpaceTypeAppService
    : CrudAppService<SpaceType, SpaceTypeDto, Guid, EstateListInput, CreateUpdateSpaceTypeDto>, ISpaceTypeAppService
{
    private readonly SpaceTypeManager _spaceTypeManager;
    private readonly IRepository<Space, Guid> _spaces;

    public SpaceTypeAppService(IRepository<SpaceType, Guid> repository, SpaceTypeManager spaceTypeManager,
        IRepository<Space, Guid> spaces)
        : base(repository)
    {
        _spaceTypeManager = spaceTypeManager;
        _spaces = spaces;
        LocalizationResource = typeof(PortalResource);
        CreatePolicyName = PortalPermissions.SpaceTypes.Create;
        UpdatePolicyName = PortalPermissions.SpaceTypes.Edit;
        DeletePolicyName = PortalPermissions.SpaceTypes.Delete;
    }

    public override async Task<SpaceTypeDto> CreateAsync(CreateUpdateSpaceTypeDto input)
    {
        await CheckCreatePolicyAsync();
        var type = await _spaceTypeManager.CreateAsync(input.Name);
        await Repository.InsertAsync(type, autoSave: true);
        return await MapToGetOutputDtoAsync(type);
    }

    public override async Task<SpaceTypeDto> UpdateAsync(Guid id, CreateUpdateSpaceTypeDto input)
    {
        await CheckUpdatePolicyAsync();
        var type = await GetEntityByIdAsync(id);
        await _spaceTypeManager.ChangeNameAsync(type, input.Name);
        await Repository.UpdateAsync(type, autoSave: true);
        return await MapToGetOutputDtoAsync(type);
    }

    public override async Task DeleteAsync(Guid id)
    {
        await CheckDeletePolicyAsync();
        var type = await GetEntityByIdAsync(id);
        await _spaceTypeManager.EnsureCanDeleteAsync(type);
        await Repository.DeleteAsync(type, autoSave: true);
    }

    protected override IQueryable<SpaceType> ApplyDefaultSorting(IQueryable<SpaceType> query) => query.OrderBy(t => t.Name);

    protected override async Task<SpaceTypeDto> MapToGetOutputDtoAsync(SpaceType entity)
        => (await MapToGetListOutputDtosAsync(new List<SpaceType> { entity }))[0];

    /* How many spaces use each type, in one grouped query. */
    protected override async Task<List<SpaceTypeDto>> MapToGetListOutputDtosAsync(List<SpaceType> entities)
    {
        var ids = entities.Select(t => t.Id).ToList();
        var counts = (await AsyncExecuter.ToListAsync((await _spaces.GetQueryableAsync())
                .Where(s => ids.Contains(s.TypeId)).GroupBy(s => s.TypeId).Select(g => new { g.Key, Count = g.Count() })))
            .ToDictionary(x => x.Key, x => x.Count);

        return entities.Select(t =>
        {
            var dto = ObjectMapper.Map<SpaceType, SpaceTypeDto>(t);
            dto.SpaceCount = counts.GetValueOrDefault(t.Id);
            return dto;
        }).ToList();
    }
}
