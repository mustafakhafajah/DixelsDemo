using System;
using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace Dixels.Portal.Common;

public class PortalUserContext : IPortalUserContext, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorization;

    public PortalUserContext(ICurrentUser currentUser, IAuthorizationService authorization)
    {
        _currentUser = currentUser;
        _authorization = authorization;
    }

    public Guid UserId => _currentUser.GetId();

    public Guid? UserIdOrNull => _currentUser.Id;

    public Task<bool> IsAdminAsync() => _authorization.IsGrantedAsync(PortalPermissions.Bookings.ManageAll);
}
