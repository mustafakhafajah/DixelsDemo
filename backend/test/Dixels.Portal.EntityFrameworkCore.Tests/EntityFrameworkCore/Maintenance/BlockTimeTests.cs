using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Maintenance;

/* "Block time": what happens to bookings under the blocked time, and long (multi-day) blocks. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BlockTimeTests : PortalEntityFrameworkCoreTestBase
{
    private readonly IMaintenanceWindowAppService _service;
    private readonly BookingManager _manager;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    public BlockTimeTests()
    {
        _service = GetRequiredService<IMaintenanceWindowAppService>();
        _manager = GetRequiredService<BookingManager>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
        _spaces = GetRequiredService<IRepository<Space, Guid>>();
    }

    [Fact]
    public async Task Bookings_under_blocked_time_are_kept_by_default()
    {
        var room = await CreateRoomAsync();
        var booking = await BookAsync(room, 0, 1);

        var r = await _service.ScheduleAsync(Block(room, Ten, Ten.AddHours(2), cancel: false));

        r.AffectedBookingsCount.ShouldBe(1);
        r.CancelledBookingsCount.ShouldBe(0);
        (await StatusOf(booking)).ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Ticking_cancel_cancels_only_the_overlapping_bookings()
    {
        var room = await CreateRoomAsync();
        var under = await BookAsync(room, 0, 1);
        var after = await BookAsync(room, 3, 4);

        var r = await _service.ScheduleAsync(Block(room, Ten, Ten.AddHours(2), cancel: true));

        r.CancelledBookingsCount.ShouldBe(1);
        (await StatusOf(under)).ShouldBe(BookingStatus.Cancelled);
        (await StatusOf(after)).ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task A_multi_day_block_stops_new_bookings_on_every_day_it_covers()
    {
        var room = await CreateRoomAsync();
        await _service.ScheduleAsync(Block(room, Ten.Date, Ten.Date.AddDays(3), cancel: false, note: "Renovation"));

        var ex = await Should.ThrowAsync<BusinessException>(() => BookAsync(room, 24 + 1, 24 + 2));
        ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceUnderMaintenance);
        ex.Message.ShouldContain("Renovation");
        await Should.NotThrowAsync(() => BookAsync(room, 24 * 3 + 1, 24 * 3 + 2));
    }

    private static ScheduleMaintenanceDto Block(Space room, DateTime start, DateTime end, bool cancel, string? note = null) => new()
    {
        ScopeType = MaintenanceScopeType.Space,
        ScopeId = room.Id,
        Note = note,
        CancelAffectedBookings = cancel,
        Occurrences = new List<TimeWindowDto> { new() { StartUtc = start, EndUtc = end } },
    };

    private Task<BookingStatus> StatusOf(Booking b) => WithUnitOfWorkAsync(async () => (await _bookings.GetAsync(b.Id)).Status);

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
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Test {tag}"), autoSave: true);
            var floor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            return await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
        });
}
