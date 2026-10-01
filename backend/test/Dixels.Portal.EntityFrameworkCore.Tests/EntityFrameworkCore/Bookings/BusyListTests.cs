using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* The busy list tells an employee only when a space is taken: no booking id, owner or name,
 * and cancelled bookings are not busy. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BusyListTests : PortalEntityFrameworkCoreTestBase
{
    private readonly BookingManager _manager;
    private readonly IBookingAppService _service;
    private readonly IRepository<Booking, Guid> _bookings;

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    public BusyListTests()
    {
        _manager = GetRequiredService<BookingManager>();
        _service = GetRequiredService<IBookingAppService>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
    }

    [Fact]
    public async Task Busy_list_has_only_space_and_times_of_confirmed_bookings_in_the_range()
    {
        var room = await CreateRoomAsync();
        var kept = await BookAsync(room, 0, 1);
        var cancelled = await BookAsync(room, 2, 3);
        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _bookings.GetAsync(cancelled.Id);
            b.Status = BookingStatus.Cancelled;
            await _bookings.UpdateAsync(b, autoSave: true);
        });
        await BookAsync(room, 30, 31); /* the next day: outside the range asked for */

        var busy = (await _service.GetBusyListAsync(new BusyListFilterDto
        {
            SpaceId = room.Id, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(12),
        })).Items;

        busy.Count.ShouldBe(1);
        busy[0].SpaceId.ShouldBe(room.Id);
        busy[0].StartUtc.ShouldBe(kept.StartUtc);
        busy[0].EndUtc.ShouldBe(kept.EndUtc);
        /* Nothing that identifies the booking or its owner is part of the shape. */
        typeof(BusyWindowDto).GetProperties().Select(p => p.Name).OrderBy(n => n)
            .ShouldBe(new[] { nameof(BusyWindowDto.EndUtc), nameof(BusyWindowDto.SpaceId), nameof(BusyWindowDto.StartUtc) });
    }

    private Task<Booking> BookAsync(Space space, double fromHours, double toHours)
        => WithUnitOfWorkAsync(async () =>
        {
            var booking = await _manager.CreateAsync(space.Id, Guid.NewGuid(), Ten.AddHours(fromHours), Ten.AddHours(toHours));
            return await _bookings.InsertAsync(booking, autoSave: true);
        });

    private Task<Space> CreateRoomAsync()
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(Guid.NewGuid(), "en", $"Busy {tag}"), autoSave: true);
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "1"), autoSave: true);
            return await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(
                new Space(Guid.NewGuid(), "en", $"Room {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
        });
}
