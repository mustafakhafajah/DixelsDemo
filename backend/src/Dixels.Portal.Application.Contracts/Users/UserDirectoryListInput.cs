using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Users;

public class UserDirectoryListInput : PagedResultRequestDto
{
    /* Matches name, surname, user name or email, ignoring case. */
    public string? Filter { get; set; }
    /* The name of an existing role, ignoring case: users in that role. */
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsLocked { get; set; }
}
