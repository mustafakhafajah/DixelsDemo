using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;

namespace Dixels.Portal.Common;

/* The one definition of "admin" in the app: a user who holds Bookings.ManageAll, whether granted directly
 * or through a role (the seeded admin role gets every permission). Everything that asks "is this an admin?"
 * comes here, so the answer can't differ between screens. */
public class PortalAdminRule : ITransientDependency
{
    public const string Permission = PortalPermissions.Bookings.ManageAll;

    private readonly IAuthorizationService _authorization;
    private readonly IPermissionFinder _permissions;

    public PortalAdminRule(IAuthorizationService authorization, IPermissionFinder permissions)
    {
        _authorization = authorization;
        _permissions = permissions;
    }

    public Task<bool> IsCurrentUserAdminAsync() => _authorization.IsGrantedAsync(Permission);

    /* Which of these users are admins, in one batched check. */
    public async Task<HashSet<Guid>> FindAdminsAsync(IEnumerable<Guid> userIds)
    {
        var requests = userIds.Select(id => new IsGrantedRequest { UserId = id, PermissionNames = new[] { Permission } }).ToList();
        if (requests.Count == 0) return new HashSet<Guid>();
        var responses = await _permissions.IsGrantedAsync(requests);
        return responses
            .Where(r => r.Permissions.TryGetValue(Permission, out var granted) && granted)
            .Select(r => r.UserId)
            .ToHashSet();
    }
}
