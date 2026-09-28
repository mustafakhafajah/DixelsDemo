using System;

namespace Dixels.Portal.Users;

public class UserDirectoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    /* The user's main role: "admin" if they have it, otherwise the first of Roles; null when they have none. */
    public string? Role { get; set; }
    /* Every role the user has, "admin" first then by name. */
    public string[] Roles { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    /* UTC; null when the user is not locked. */
    public DateTime? LockoutEnd { get; set; }
    public bool IsLocked { get; set; }
}
