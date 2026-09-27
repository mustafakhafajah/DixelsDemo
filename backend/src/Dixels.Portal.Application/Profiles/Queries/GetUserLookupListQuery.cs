using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Cqrs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace Dixels.Portal.Profiles.Queries;

/* Every user with a display name and an admin flag, for admin pickers ("book on behalf of"). */
public record GetUserLookupListQuery : IQuery<ListResultDto<UserLookupDto>>;

public class GetUserLookupListQueryHandler : IQueryHandler<GetUserLookupListQuery, ListResultDto<UserLookupDto>>
{
    private readonly IIdentityUserRepository _users;

    public GetUserLookupListQueryHandler(IIdentityUserRepository users)
    {
        _users = users;
    }

    public async Task<ListResultDto<UserLookupDto>> HandleAsync(GetUserLookupListQuery query)
    {
        var users = await _users.GetListAsync(includeDetails: true);
        var adminRoles = await _users.GetRoleNamesAsync(users.Select(u => u.Id));
        var admins = adminRoles.Where(r => r.RoleNames.Contains("admin")).Select(r => r.Id).ToHashSet();
        return new ListResultDto<UserLookupDto>(users
            .OrderBy(u => u.GetDisplayName())
            .Select(u => new UserLookupDto { Id = u.Id, Name = u.GetDisplayName(), IsAdmin = admins.Contains(u.Id) })
            .ToList());
    }
}
