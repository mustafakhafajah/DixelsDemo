using System;

namespace Dixels.Portal.Users;

public class UserDirectoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    /* "admin", "employee", or null when the user has neither role. */
    public string? Role { get; set; }
    public bool IsActive { get; set; }
    /* UTC; null when the user is not locked. */
    public DateTime? LockoutEnd { get; set; }
    public bool IsLocked { get; set; }
}
