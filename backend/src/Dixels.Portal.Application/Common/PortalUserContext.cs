using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace Dixels.Portal.Common;

public class PortalUserContext : IPortalUserContext, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly PortalAdminRule _adminRule;

    public PortalUserContext(ICurrentUser currentUser, PortalAdminRule adminRule)
    {
        _currentUser = currentUser;
        _adminRule = adminRule;
    }

    public Guid UserId => _currentUser.GetId();

    public Guid? UserIdOrNull => _currentUser.Id;

    public Task<bool> IsAdminAsync() => _adminRule.IsCurrentUserAdminAsync();
}
