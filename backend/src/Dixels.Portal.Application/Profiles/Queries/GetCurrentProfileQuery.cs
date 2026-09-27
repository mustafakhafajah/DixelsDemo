using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Cqrs;
using Volo.Abp.Identity;

namespace Dixels.Portal.Profiles.Queries;

/* The signed-in user, as the SPA shows them (name, email, admin or not). */
public record GetCurrentProfileQuery : IQuery<CurrentUserProfileDto>;

public class GetCurrentProfileQueryHandler : IQueryHandler<GetCurrentProfileQuery, CurrentUserProfileDto>
{
    private readonly IIdentityUserRepository _users;
    private readonly IPortalUserContext _user;

    public GetCurrentProfileQueryHandler(IIdentityUserRepository users, IPortalUserContext user)
    {
        _users = users;
        _user = user;
    }

    public async Task<CurrentUserProfileDto> HandleAsync(GetCurrentProfileQuery query)
    {
        var user = await _users.GetAsync(_user.UserId, includeDetails: false);
        return new CurrentUserProfileDto
        {
            Id = user.Id,
            Name = user.GetDisplayName(),
            Email = user.Email,
            IsAdmin = await _user.IsAdminAsync(),
        };
    }
}
