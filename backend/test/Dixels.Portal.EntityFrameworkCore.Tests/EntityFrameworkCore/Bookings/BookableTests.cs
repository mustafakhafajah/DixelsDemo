using System;
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

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* The "Bookable" tick on buildings, floors and spaces, and the admin's "cancel upcoming bookings" action. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BookableTests : PortalEntityFrameworkCoreTestBase
{
    private readonly BookingManager _manager;
    private readonly IBookingAppService _bookingService;
    private readonly ISpaceAppService _spaceService;
    private readonly IBuildingAppService _buildingService;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    public BookableTests()
    {
        _manager = GetRequiredService<BookingManager>();
        _bookingService = GetRequiredService<IBookingAppService>();
        _spaceService = GetRequiredService<ISpaceAppService>();
        _buildingService = GetRequiredService<IBuildingAppService>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
        _spaces = GetRequiredService<IRepository<Space, Guid>>();
    }

    [Fact]
    public async Task A_space_in_a_building_that_is_not_bookable_cannot_be_booked_and_says_why()
    {
        var e = await CreateEstateAsync();
        await _buildingService.SetBookableAsync(e.Building.Id, new SetBookableDto { IsBookable = false });

        var ex = await Should.ThrowAsync<BusinessException>(() => BookAsync(e.Room, 0, 1));
        ex.Code.ShouldBe(PortalDomainErrorCodes.BuildingNotBookable);

        /* The space itself is still ticked, but the DTO tells the truth about why it can't be booked. */
        var dto = await _spaceService.GetAsync(e.Room.Id);
        dto.IsBookable.ShouldBeTrue();
        dto.CanCurrentUserBook.ShouldBeFalse();
        dto.NotBookableReason!.ShouldContain(e.Building.Name);
    }

    [Fact]
    public async Task Ticking_bookable_again_allows_booking()
    {
        var e = await CreateEstateAsync();
        await _spaceService.SetBookableAsync(e.Room.Id, new SetBookableDto { IsBookable = false });
        (await Should.ThrowAsync<BusinessException>(() => BookAsync(e.Room, 0, 1))).Code.ShouldBe(PortalDomainErrorCodes.SpaceNotBookable);

        await _spaceService.SetBookableAsync(e.Room.Id, new SetBookableDto { IsBookable = true });

        await Should.NotThrowAsync(() => BookAsync(e.Room, 0, 1));
        (await _spaceService.GetAsync(e.Room.Id)).NotBookableReason.ShouldBeNull();
    }

    [Fact]
    public async Task Making_something_not_bookable_keeps_existing_bookings()
    {
        var e = await CreateEstateAsync();
        var booking = await BookAsync(e.Room, 0, 1);

        await _buildingService.SetBookableAsync(e.Building.Id, new SetBookableDto { IsBookable = false });

        (await WithUnitOfWorkAsync(() => _bookings.GetAsync(booking.Id))).Status.ShouldBe(BookingStatus.Confirmed);
    }

    [Fact]
    public async Task Counts_and_cancels_only_upcoming_bookings_in_the_scope()
    {
        var e = await CreateEstateAsync();
        var upcoming1 = await BookAsync(e.Room, 0, 1);
        var upcoming2 = await BookAsync(e.Room, 2, 3);
        var elsewhere = await BookAsync(e.OtherBuildingRoom, 0, 1);
        /* One that already started: it must survive the cancel. */
        var started = await BookAsync(e.Room, 4, 5);
        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _bookings.GetAsync(started.Id);
            b.StartUtc = DateTime.UtcNow.AddMinutes(-30);
            b.EndUtc = DateTime.UtcNow.AddMinutes(30);
            await _bookings.UpdateAsync(b, autoSave: true);
        });
        var scope = new EstateScopeDto { ScopeType = MaintenanceScopeType.Building, ScopeId = e.Building.Id };

        (await _bookingService.GetUpcomingCountAsync(scope)).ShouldBe(2);
        (await _bookingService.CancelUpcomingAsync(scope)).CancelledCount.ShouldBe(2);

        (await StatusOf(upcoming1)).ShouldBe(BookingStatus.Cancelled);
        (await StatusOf(upcoming2)).ShouldBe(BookingStatus.Cancelled);
        (await StatusOf(started)).ShouldBe(BookingStatus.Confirmed);
        (await StatusOf(elsewhere)).ShouldBe(BookingStatus.Confirmed);
        (await _bookingService.GetUpcomingCountAsync(scope)).ShouldBe(0);
    }

    private Task<BookingStatus> StatusOf(Booking b) => WithUnitOfWorkAsync(async () => (await _bookings.GetAsync(b.Id)).Status);

    private Task<Booking> BookAsync(Space space, double fromHours, double toHours)
        => WithUnitOfWorkAsync(async () =>
        {
            var booking = await _manager.CreateAsync(space.Id, Guid.NewGuid(), Ten.AddHours(fromHours), Ten.AddHours(toHours));
            return await _bookings.InsertAsync(booking, autoSave: true);
        });

    private record Estate(Building Building, Space Room, Space OtherBuildingRoom);

    private Task<Estate> CreateEstateAsync()
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Test {tag}"), autoSave: true);
            var floor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            var room = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
            var other = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Other {tag}"), autoSave: true);
            var otherFloor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), other.Id, "1"), autoSave: true);
            var otherRoom = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Other room {tag}", other.Id, otherFloor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
            return new Estate(building, room, otherRoom);
        });
}
