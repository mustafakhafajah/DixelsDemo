using System;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Users;

/* One page of the user directory. Every filter is optional; leaving one out means "any". */
public class GetUserDirectoryInput : PagedResultRequestDto
{
    /* Part of the user name, email, name, surname or phone number. */
    public string? Filter { get; set; }

    public Guid? RoleId { get; set; }

    /* true = active accounts only, false = inactive only. */
    public bool? IsActive { get; set; }

    /* true = locked out right now, false = not locked. */
    public bool? IsLocked { get; set; }
}
