using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Estate;

public class CurrentUserProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
}

public class UserLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsAdmin { get; set; }
}
