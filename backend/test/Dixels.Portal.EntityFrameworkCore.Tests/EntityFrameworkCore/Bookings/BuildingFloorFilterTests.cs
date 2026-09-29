using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Maintenance;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* The Schedule's Building and Floor filters are answered by the server: the booking list, the busy list
 * and the blocked-time list return only what is on spaces of that building / floor. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BuildingFloorFilterTests : PortalEntityFrameworkCoreTestBase
{
    private readonly BookingManager _manager;
    private readonly IBookingAppService _bookingService;
    private readonly IMaintenanceWindowAppService _maintenanceService;
    private readonly IRepository<Booking, Guid> _bookings;

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(40).AddHours(10);

    public BuildingFloorFilterTests()
    {
        _manager = GetRequiredService<BookingManager>();
        _bookingService = GetRequiredService<IBookingAppService>();
        _maintenanceService = GetRequiredService<IMaintenanceWindowAppService>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
    }

    [Fact]
    public async Task Booking_list_by_building_or_floor_returns_only_bookings_there()
    {
        var e = await CreateEstateAsync();
        foreach (var s in e.All) await BookAsync(s);

        var byBuilding = (await _bookingService.GetListAsync(new BookingListFilterDto
        {
            BuildingId = e.BuildingA, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(3),
        })).Items;
        byBuilding.Select(b => b.SpaceId).ShouldBe(new[] { e.A1.Id, e.A2.Id }, ignoreOrder: true);

        var byFloor = (await _bookingService.GetListAsync(new BookingListFilterDto
        {
            FloorId = e.FloorA2, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(3),
        })).Items;
        byFloor.Select(b => b.SpaceId).ShouldBe(new[] { e.A2.Id });
    }

    [Fact]
    public async Task Busy_list_by_building_or_floor_returns_only_busy_times_there()
    {
        var e = await CreateEstateAsync();
        foreach (var s in e.All) await BookAsync(s);

        var byBuilding = (await _bookingService.GetBusyListAsync(new BusyListFilterDto
        {
            BuildingId = e.BuildingB, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(3),
        })).Items;
        byBuilding.Select(b => b.SpaceId).ShouldBe(new[] { e.B1.Id });

        var byFloor = (await _bookingService.GetBusyListAsync(new BusyListFilterDto
        {
            FloorId = e.FloorA1, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(3),
        })).Items;
        byFloor.Select(b => b.SpaceId).ShouldBe(new[] { e.A1.Id });
    }

    [Fact]
    public async Task Blocked_time_list_by_building_or_floor_returns_only_windows_there()
    {
        var e = await CreateEstateAsync();
        foreach (var s in e.All) await BlockAsync(s);

        var byBuilding = (await _maintenanceService.GetListAsync(new MaintenanceListFilterDto
        {
            BuildingId = e.BuildingA, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(3),
        })).Items;
        byBuilding.Select(m => m.SpaceId).ShouldBe(new[] { e.A1.Id, e.A2.Id }, ignoreOrder: true);

        var byFloor = (await _maintenanceService.GetListAsync(new MaintenanceListFilterDto
        {
            FloorId = e.FloorA1, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(3),
        })).Items;
        byFloor.Select(m => m.SpaceId).ShouldBe(new[] { e.A1.Id });
    }

    private Task BookAsync(Space space)
        => WithUnitOfWorkAsync(async () =>
        {
            var booking = await _manager.CreateAsync(space.Id, Guid.NewGuid(), Ten, Ten.AddHours(1));
            await _bookings.InsertAsync(booking, autoSave: true);
        });

    private Task BlockAsync(Space space)
        => _maintenanceService.ScheduleAsync(new ScheduleMaintenanceDto
        {
            ScopeType = MaintenanceScopeType.Space,
            ScopeId = space.Id,
            Occurrences = new List<TimeWindowDto> { new() { StartUtc = Ten, EndUtc = Ten.AddHours(2) } },
        });

    private sealed record TestEstate(Guid BuildingA, Guid FloorA1, Guid FloorA2, Guid BuildingB, Space A1, Space A2, Space B1)
    {
        public Space[] All => new[] { A1, A2, B1 };
    }

    /* Building A with two floors (one room each) and building B with one floor and one room. */
    private Task<TestEstate> CreateEstateAsync()
        => WithUnitOfWorkAsync(async () =>
        {
            var buildings = GetRequiredService<IRepository<Building, Guid>>();
            var floors = GetRequiredService<IRepository<Floor, Guid>>();
            var spaces = GetRequiredService<IRepository<Space, Guid>>();
            var tag = Guid.NewGuid().ToString("N")[..8];

            var a = await buildings.InsertAsync(new Building(Guid.NewGuid(), $"Filter A {tag}"), autoSave: true);
            var b = await buildings.InsertAsync(new Building(Guid.NewGuid(), $"Filter B {tag}"), autoSave: true);
            var a1 = await floors.InsertAsync(new Floor(Guid.NewGuid(), a.Id, "1"), autoSave: true);
            var a2 = await floors.InsertAsync(new Floor(Guid.NewGuid(), a.Id, "2"), autoSave: true);
            var b1 = await floors.InsertAsync(new Floor(Guid.NewGuid(), b.Id, "1"), autoSave: true);

            Task<Space> Room(string name, Guid buildingId, Guid floorId) => spaces.InsertAsync(
                new Space(Guid.NewGuid(), $"{name} {tag}", buildingId, floorId, DefaultSpaceTypes.MeetingRoom), autoSave: true);

            return new TestEstate(a.Id, a1.Id, a2.Id, b.Id,
                await Room("A1", a.Id, a1.Id), await Room("A2", a.Id, a2.Id), await Room("B1", b.Id, b1.Id));
        });
}
