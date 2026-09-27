using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Permissions;
using Dixels.Portal.Profiles.Queries;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Profiles;

/* Read-only: this service has queries and no commands. */
[Authorize]
public class ProfileLookupAppService : PortalAppService, IProfileLookupAppService
{
    private readonly IQueryDispatcher _queries;

    public ProfileLookupAppService(IQueryDispatcher queries)
    {
        _queries = queries;
    }

    public Task<CurrentUserProfileDto> GetCurrentAsync()
        => _queries.QueryAsync(new GetCurrentProfileQuery());

    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public Task<ListResultDto<UserLookupDto>> GetUsersAsync()
        => _queries.QueryAsync(new GetUserLookupListQuery());
}
