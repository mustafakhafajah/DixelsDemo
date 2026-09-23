using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Dixels.Portal.Estate;

[Authorize]
public class TeamAppService : EstateAppServiceBase, ITeamAppService
{
    private readonly IRepository<Team, Guid> _teams;
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IIdentityUserRepository _users;

    public TeamAppService(IRepository<Team, Guid> teams, IRepository<Space, Guid> spaces, IIdentityUserRepository users)
    {
        _teams = teams;
        _spaces = spaces;
        _users = users;
    }

    public async Task<ListResultDto<TeamDto>> GetListAsync()
    {
        var teams = await _teams.GetListAsync();
        return new ListResultDto<TeamDto>(teams.OrderBy(t => t.Name).Select(Map).ToList());
    }

    [Authorize(PortalPermissions.Teams.Manage)]
    public async Task<TeamDto> CreateAsync(CreateUpdateTeamDto input)
    {
        await EnsureUniqueAsync(input.Name, null);
        var team = await _teams.InsertAsync(new Team(GuidGenerator.Create(), input.Name.Trim()), autoSave: true);
        return Map(team);
    }

    [Authorize(PortalPermissions.Teams.Manage)]
    public async Task<TeamDto> UpdateAsync(Guid id, CreateUpdateTeamDto input)
    {
        var team = await _teams.GetAsync(id);
        await EnsureUniqueAsync(input.Name, id);
        team.Name = input.Name.Trim();
        return Map(await _teams.UpdateAsync(team, autoSave: true));
    }

    [Authorize(PortalPermissions.Teams.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        var spaces = await _spaces.GetListAsync();
        var users = await _users.GetListAsync();
        if (spaces.Any(s => s.RestrictedTeamIds.Contains(id)) ||
            users.Any(u => u.GetProperty<Guid?>(BookingManager.TeamIdProperty) == id))
            throw new BusinessException(PortalDomainErrorCodes.TeamInUse,
                "This team is still used by a space restriction or a user.");
        await _teams.DeleteAsync(id);
    }

    private async Task EnsureUniqueAsync(string name, Guid? excludeId)
    {
        var normalized = name.Trim().ToLower();
        if (await _teams.AnyAsync(t => t.Name.ToLower() == normalized && t.Id != excludeId))
            throw new BusinessException(PortalDomainErrorCodes.TeamDuplicate, $"A team called \"{name}\" already exists.");
    }

    private static TeamDto Map(Team t) => new() { Id = t.Id, Name = t.Name };
}
