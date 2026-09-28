using System;

namespace Dixels.Portal.Users;

public class LockUserDto
{
    /* UTC. Null locks the user until they are unlocked. */
    public DateTime? Until { get; set; }
}
