using System;
using System.Threading.Tasks;

namespace Dixels.Portal.Common;

/* Who is making the request, as handlers need it. Handlers are not app services, so they don't
 * inherit CurrentUser/AuthorizationService; they depend on this instead (and tests can fake it). */
public interface IPortalUserContext
{
    /* The signed-in user's id; throws if nobody is signed in. */
    Guid UserId { get; }

    Guid? UserIdOrNull { get; }

    /* Admins hold Bookings.ManageAll; the admin role is granted every permission by the identity seeder. */
    Task<bool> IsAdminAsync();
}
