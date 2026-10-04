using System;

namespace Dixels.Portal.Profiles;

public class UserLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    /* In ABP's admin role; only a label, never a permission. */
    public bool IsAdmin { get; set; }
}
