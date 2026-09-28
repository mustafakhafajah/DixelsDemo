using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Users;

public class UserDirectoryListInput : PagedResultRequestDto
{
    /* Matches name, surname, user name or email, ignoring case. */
    public string? Filter { get; set; }
    /* "admin" or "employee". */
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsLocked { get; set; }
}
