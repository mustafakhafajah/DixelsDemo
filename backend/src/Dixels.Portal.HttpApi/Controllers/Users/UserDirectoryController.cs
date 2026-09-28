using System;
using System.Threading.Tasks;
using Dixels.Portal.Users;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Controllers.Users;

/* Admin: the user directory. */
[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/user-directory")]
public class UserDirectoryController : PortalController, IUserDirectoryAppService
{
    private readonly IUserDirectoryAppService _users;

    public UserDirectoryController(IUserDirectoryAppService users)
    {
        _users = users;
    }

    [HttpGet]
    public Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync([FromQuery] UserDirectoryListInput input) => _users.GetListAsync(input);

    /* Every role a user can be given, admin first. */
    [HttpGet("roles")]
    public Task<ListResultDto<UserDirectoryRoleDto>> GetRolesAsync() => _users.GetRolesAsync();

    [HttpPost]
    public Task<UserDirectoryItemDto> CreateAsync([FromBody] CreateUserDirectoryDto input) => _users.CreateAsync(input);

    [HttpPost("{id}/role")]
    public Task<UserDirectoryItemDto> SetRoleAsync(Guid id, [FromBody] SetUserRoleDto input) => _users.SetRoleAsync(id, input);

    [HttpPost("{id}/lock")]
    public Task<UserDirectoryItemDto> LockAsync(Guid id, [FromBody] LockUserDto input) => _users.LockAsync(id, input);

    [HttpPost("{id}/unlock")]
    public Task<UserDirectoryItemDto> UnlockAsync(Guid id) => _users.UnlockAsync(id);

    [HttpPost("{id}/active")]
    public Task<UserDirectoryItemDto> SetActiveAsync(Guid id, [FromBody] SetUserActiveDto input) => _users.SetActiveAsync(id, input);

    /* Every portal permission with what this user ends up with and why. */
    [HttpGet("{id}/permissions")]
    public Task<ListResultDto<UserPermissionDto>> GetPermissionsAsync(Guid id) => _users.GetPermissionsAsync(id);

    [HttpPut("{id}/permissions")]
    public Task<ListResultDto<UserPermissionDto>> UpdatePermissionsAsync(Guid id, [FromBody] UpdateUserPermissionsDto input) => _users.UpdatePermissionsAsync(id, input);
}
