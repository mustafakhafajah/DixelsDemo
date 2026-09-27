using System;

namespace Dixels.Portal.Profiles;

public class CurrentUserProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
}
