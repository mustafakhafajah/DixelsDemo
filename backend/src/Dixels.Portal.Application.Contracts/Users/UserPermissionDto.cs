namespace Dixels.Portal.Users;

public class UserPermissionDto
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? ParentName { get; set; }
    public string GroupName { get; set; } = null!;
    /* What the user ends up with. */
    public bool IsGranted { get; set; }
    /* One of the user's roles grants it. */
    public bool FromRole { get; set; }
    /* One of UserPermissionSources. */
    public string Source { get; set; } = null!;
}
