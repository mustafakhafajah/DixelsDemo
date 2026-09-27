using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Spaces;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Timing;

namespace Dixels.Portal.Bookings;

/* Turns bookings into BookingDto. A booking DTO shows the space name and owner name,
 * so mapping needs look-ups; keeping them here leaves BookingAppService with only the use cases. */
public class BookingDtoMapper : ITransientDependency
{
    private readonly IRepository<Space, Guid> _spaces;
    private readonly IIdentityUserRepository _users;
    private readonly IClock _clock;

    public BookingDtoMapper(IRepository<Space, Guid> spaces, IIdentityUserRepository users, IClock clock)
    {
        _spaces = spaces;
        _users = users;
        _clock = clock;
    }

    public async Task<List<BookingDto>> MapListAsync(IReadOnlyCollection<Booking> list)
    {
        if (list.Count == 0) return new();
        /* Load only the spaces and users this list refers to, once each. */
        var spaceIds = list.Select(b => b.SpaceId).Distinct().ToList();
        var spaces = (await _spaces.GetListAsync(s => spaceIds.Contains(s.Id))).ToDictionary(s => s.Id, s => s.Name);
        var userIds = list.Select(b => b.OwnerUserId).Distinct().ToList();
        var users = (await _users.GetListByIdsAsync(userIds)).ToDictionary(u => u.Id, u => u.GetDisplayName());
        var now = _clock.Now;
        return list.Select(b => new BookingDto
        {
            Id = b.Id,
            SpaceId = b.SpaceId,
            SpaceName = spaces.GetValueOrDefault(b.SpaceId, "Unknown space"),
            OwnerUserId = b.OwnerUserId,
            OwnerName = users.GetValueOrDefault(b.OwnerUserId, "a former user"),
            StartUtc = b.StartUtc,
            EndUtc = b.EndUtc,
            Status = b.Status,
            Lifecycle = b.GetLifecycle(now),
            Version = b.Version,
            SeriesId = b.SeriesId,
            Parking = b.Parking,
            CreationTime = b.CreationTime,
            LastModificationTime = b.LastModificationTime,
        }).ToList();
    }

    public async Task<BookingDto> MapAsync(Booking booking) => (await MapListAsync(new[] { booking }))[0];
}
