using System.ComponentModel.DataAnnotations;

namespace Dixels.Portal.Users;

public class CreateUserDirectoryDto
{
    [Required] [StringLength(64)] public string Name { get; set; } = null!;
    [Required] [EmailAddress] [StringLength(256)] public string Email { get; set; } = null!;
    /* Defaults to the email. */
    [StringLength(256)] public string? UserName { get; set; }
    [Required] [StringLength(128)] public string Password { get; set; } = null!;
    /* "admin" or "employee". */
    [Required] public string Role { get; set; } = null!;
}
