using System;

namespace Dixels.Portal.Profiles;

public class UserLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsAdmin { get; set; }
}
