using System;
using System.Collections.Generic;

namespace Dixels.Portal.Profiles;

public class UserLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    /* The person's role names (e.g. "admin", "employee"), shown next to the name; never used as a permission. */
    public List<string> Roles { get; set; } = new();
}
