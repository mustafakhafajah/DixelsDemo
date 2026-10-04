using System;
using System.Collections.Generic;

namespace Dixels.Portal.Profiles;

public class CurrentUserProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    /* The user's role names as stored in the identity database (e.g. "admin", "employee"). */
    public List<string> Roles { get; set; } = new();
}
