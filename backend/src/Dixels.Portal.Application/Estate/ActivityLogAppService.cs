using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Dixels.Portal.Estate;

[Authorize]
public class ActivityLogAppService : EstateAppServiceBase, IActivityLogAppService
{
    private readonly IRepository<ActivityLogEntry, Guid> _entries;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly ActivityLogAppender _log;

    public ActivityLogAppService(IRepository<ActivityLogEntry, Guid> entries, IRepository<Booking, Guid> bookings,
        ActivityLogAppender log)
    {
        _entries = entries;
        _bookings = bookings;
        _log = log;
    }

    public async Task<ListResultDto<ActivityLogEntryDto>> GetListAsync(ActivityLogFilterDto input)
    {
        var isAdmin = await IsAdminAsync();
        if (string.IsNullOrWhiteSpace(input.EntityId))
        {
            if (!await AuthorizationService.IsGrantedAsync(PortalPermissions.ActivityLog.ViewAll))
                throw new AbpAuthorizationException("Only an administrator can read the full activity log.");
        }
        else if (!isAdmin)
        {
            /* Non-admins may read the trail of their own bookings only. */
            var ok = Guid.TryParse(input.EntityId, out var id) &&
                     await _bookings.AnyAsync(b => b.Id == id && b.OwnerUserId == CurrentUser.Id);
            if (!ok)
                throw new AbpAuthorizationException("You can only read the history of your own bookings.");
        }

        var query = await _entries.GetQueryableAsync();
        if (!string.IsNullOrWhiteSpace(input.EntityId)) query = query.Where(e => e.EntityId == input.EntityId);
        if (!string.IsNullOrWhiteSpace(input.Action)) query = query.Where(e => e.Action == input.Action);
        var list = await AsyncExecuter.ToListAsync(query.OrderByDescending(e => e.TimestampUtc).Take(input.MaxResultCount));
        return new ListResultDto<ActivityLogEntryDto>(list.Select(e => new ActivityLogEntryDto
        {
            Id = e.Id,
            TimestampUtc = e.TimestampUtc,
            ActorUserId = e.ActorUserId,
            ActorName = e.ActorName,
            Action = e.Action,
            EntityType = e.EntityType,
            EntityId = e.EntityId,
            Detail = e.Detail,
        }).ToList());
    }

    public async Task RecordSignInAsync()
    {
        var role = await IsAdminAsync() ? "administrator" : "booking_user";
        await _log.LogAsync(ActivityActions.SessionSignedIn, ActivityEntityTypes.Session, CurrentUser.GetId(),
            $"{CurrentUser.Name ?? CurrentUser.UserName} signed in as {role}");
    }
}

[Authorize]
public class ProfileLookupAppService : EstateAppServiceBase, IProfileLookupAppService
{
    private readonly IIdentityUserRepository _users;
    private readonly IRepository<Team, Guid> _teams;
    private readonly BookingManager _manager;

    public ProfileLookupAppService(IIdentityUserRepository users, IRepository<Team, Guid> teams, BookingManager manager)
    {
        _users = users;
        _teams = teams;
        _manager = manager;
    }

    public async Task<CurrentUserProfileDto> GetCurrentAsync()
    {
        var user = await _users.GetAsync(CurrentUser.GetId(), includeDetails: false);
        var teamId = await _manager.GetUserTeamIdAsync(user.Id);
        var team = teamId.HasValue ? await _teams.FindAsync(teamId.Value) : null;
        return new CurrentUserProfileDto
        {
            Id = user.Id,
            Name = BookingAppService.DisplayName(user),
            Email = user.Email,
            TeamId = team?.Id,
            TeamName = team?.Name,
            IsAdmin = await IsAdminAsync(),
        };
    }

    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public async Task<ListResultDto<UserLookupDto>> GetUsersAsync()
    {
        var users = await _users.GetListAsync(includeDetails: true);
        var adminRoles = await _users.GetRoleNamesAsync(users.Select(u => u.Id));
        var admins = adminRoles.Where(r => r.RoleNames.Contains("admin")).Select(r => r.Id).ToHashSet();
        return new ListResultDto<UserLookupDto>(users
            .OrderBy(BookingAppService.DisplayName)
            .Select(u => new UserLookupDto { Id = u.Id, Name = BookingAppService.DisplayName(u), IsAdmin = admins.Contains(u.Id) })
            .ToList());
    }
}
