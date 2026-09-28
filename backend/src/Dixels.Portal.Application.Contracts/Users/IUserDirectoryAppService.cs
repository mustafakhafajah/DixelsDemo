using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Users;

public interface IUserDirectoryAppService : IApplicationService
{
    Task<PagedResultDto<UserDirectoryItemDto>> GetListAsync(UserDirectoryListInput input);
    Task<UserDirectoryItemDto> CreateAsync(CreateUserDirectoryDto input);
    Task<UserDirectoryItemDto> SetRoleAsync(Guid id, SetUserRoleDto input);
    Task<UserDirectoryItemDto> LockAsync(Guid id, LockUserDto input);
    Task<UserDirectoryItemDto> UnlockAsync(Guid id);
    Task<UserDirectoryItemDto> SetActiveAsync(Guid id, SetUserActiveDto input);
    Task<ListResultDto<UserPermissionDto>> GetPermissionsAsync(Guid id);
    Task<ListResultDto<UserPermissionDto>> UpdatePermissionsAsync(Guid id, UpdateUserPermissionsDto input);
}
