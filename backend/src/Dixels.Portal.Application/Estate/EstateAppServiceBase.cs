using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Dixels.Portal.Estate;

public abstract class EstateAppServiceBase : PortalAppService
{
    /* Admins hold ManageAll; the admin role is granted every permission by the identity seeder. */
    protected Task<bool> IsAdminAsync()
        => AuthorizationService.IsGrantedAsync(PortalPermissions.Bookings.ManageAll);
}
