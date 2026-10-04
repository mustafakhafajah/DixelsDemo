using System;
using System.Collections.Generic;

namespace Dixels.Portal.Users;

public class UserDirectoryItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    /* When the lock ends; null when the account is not locked. */
    public DateTimeOffset? LockoutEnd { get; set; }
    public List<string> Roles { get; set; } = new();
}
