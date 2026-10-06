using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* The domain tests prove the overlap rule in memory; these prove EF can translate it to SQL
 * (the specification combined with extra conditions) and that the factory enforces the rules. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BookingManagerTests : PortalEntityFrameworkCoreTestBase
{
    private readonly BookingManager _manager;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;
    private readonly IRepository<Space, Guid> _spaces;

    /* A weekday-independent slot well in the future, inside the default 08:00-20:00 hours. */
    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    public BookingManagerTests()
    {
        _manager = GetRequiredService<BookingManager>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
        _spaces = GetRequiredService<IRepository<Space, Guid>>();
    }

    [Fact]
    public async Task Rejects_an_overlapping_booking_on_the_same_space()
    {
        var (roomA, _) = await CreateTwoSpacesAsync();
        await BookAsync(roomA, Guid.NewGuid(), 0, 1);

        var ex = await Should.ThrowAsync<BusinessException>(() => BookAsync(roomA, Guid.NewGuid(), 0.5, 1.5));
        ex.Code.ShouldBe(PortalDomainErrorCodes.BookingConflict);
        ex.Data[ErrorFieldExtensions.FieldKey].ShouldBe("window");
    }

    [Fact]
    public async Task Allows_a_booking_that_starts_when_the_previous_one_ends()
    {
        var (roomA, _) = await CreateTwoSpacesAsync();
        await BookAsync(roomA, Guid.NewGuid(), 0, 1);

        await Should.NotThrowAsync(() => BookAsync(roomA, Guid.NewGuid(), 1, 2));
    }

    [Fact]
    public async Task Rejects_the_same_person_holding_two_spaces_at_once()
    {
        var (roomA, roomB) = await CreateTwoSpacesAsync();
        var person = Guid.NewGuid();
        await BookAsync(roomA, person, 0, 1);

        var ex = await Should.ThrowAsync<BusinessException>(() => BookAsync(roomB, person, 0.5, 1.5));
        ex.Code.ShouldBe(PortalDomainErrorCodes.BookingSelfOverlap);
    }

    [Fact]
    public async Task An_owner_allowed_several_spaces_can_hold_two_at_once()
    {
        var (roomA, roomB) = await CreateTwoSpacesAsync();
        var admin = Guid.NewGuid();
        await BookAsync(roomA, admin, 0, 1, ownerMayHoldSeveralSpaces: true);

        await Should.NotThrowAsync(() => BookAsync(roomB, admin, 0.5, 1.5, ownerMayHoldSeveralSpaces: true));
    }

    [Fact]
    public async Task Holding_several_spaces_still_never_double_books_one_space()
    {
        var (roomA, _) = await CreateTwoSpacesAsync();
        await BookAsync(roomA, Guid.NewGuid(), 0, 1);

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            BookAsync(roomA, Guid.NewGuid(), 0.5, 1.5, ownerMayHoldSeveralSpaces: true));
        ex.Code.ShouldBe(PortalDomainErrorCodes.BookingConflict);
    }

    [Fact]
    public async Task Specification_combines_with_other_conditions_in_a_count_query()
    {
        var (roomA, roomB) = await CreateTwoSpacesAsync();
        await BookAsync(roomA, Guid.NewGuid(), 0, 1);
        await BookAsync(roomB, Guid.NewGuid(), 0, 1);
        var onlyA = new List<Guid> { roomA.Id };

        var count = await WithUnitOfWorkAsync(() => _bookings.CountAsync(
            new OverlappingBookingsSpecification(Ten.AddHours(0.5), Ten.AddHours(1.5))
                .ToExpression().And(b => onlyA.Contains(b.SpaceId))));

        count.ShouldBe(1);
    }

    [Fact]
    public async Task Refuses_a_booking_on_a_weekly_closed_day()
    {
        var (roomA, _) = await CreateTwoSpacesAsync(b => b.ClosedWeekdays = new List<int> { (int)Ten.DayOfWeek });

        var ex = await Should.ThrowAsync<BusinessException>(() => BookAsync(roomA, Guid.NewGuid(), 0, 1));
        ex.Code.ShouldBe(PortalDomainErrorCodes.HolidayClosed);
        ex.Data[ErrorFieldExtensions.FieldKey].ShouldBe("date");
    }

    [Fact]
    public async Task Allows_a_booking_on_an_open_day_of_a_building_with_closed_days()
    {
        var (roomA, _) = await CreateTwoSpacesAsync(b => b.ClosedWeekdays = new List<int> { (int)Ten.AddDays(1).DayOfWeek });

        await Should.NotThrowAsync(() => BookAsync(roomA, Guid.NewGuid(), 0, 1));
    }

    /* Riyadh is UTC+3: 08:00-18:00 there is 05:00-15:00 UTC. Ten is 10:00 UTC (13:00 in Riyadh). */
    [Fact]
    public async Task Opening_hours_are_checked_on_the_buildings_own_clock()
    {
        var (roomA, roomB) = await CreateTwoSpacesAsync(b =>
        {
            b.TimeZone = "Asia/Riyadh";
            b.CloseHour = 18;
        });

        await Should.NotThrowAsync(() => BookAsync(roomA, Guid.NewGuid(), -5, -4));    // 08:00-09:00 in Riyadh
        var ex = await Should.ThrowAsync<BusinessException>(() => BookAsync(roomB, Guid.NewGuid(), 6, 7));  // 19:00-20:00 in Riyadh
        ex.Code.ShouldBe(PortalDomainErrorCodes.OutsideHours);
        /* Times are picked in UTC, so the message gives the local hours with the zone, and the same hours in UTC. */
        ex.Message.ShouldContain("08:00");
        ex.Message.ShouldContain("Riyadh");
        ex.Message.ShouldContain("05:00");
        ex.Message.ShouldContain("15:00 UTC");
    }

    [Fact]
    public async Task A_utc_building_gives_its_hours_without_a_zone()
    {
        var (roomA, _) = await CreateTwoSpacesAsync(b =>
        {
            b.TimeZone = "UTC";
            b.CloseHour = 18;
        });

        var ex = await Should.ThrowAsync<BusinessException>(() => BookAsync(roomA, Guid.NewGuid(), 9, 10));  // 19:00-20:00 UTC
        ex.Code.ShouldBe(PortalDomainErrorCodes.OutsideHours);
        ex.Message.ShouldContain("18:00");
        ex.Message.ShouldNotContain("UTC");
    }

    private Task BookAsync(Space space, Guid ownerId, double fromHours, double toHours, bool ownerMayHoldSeveralSpaces = false)
        => WithUnitOfWorkAsync(async () =>
        {
            var booking = await _manager.CreateAsync(space.Id, ownerId, Ten.AddHours(fromHours), Ten.AddHours(toHours),
                ownerMayHoldSeveralSpaces: ownerMayHoldSeveralSpaces);
            await _bookings.InsertAsync(booking, autoSave: true);
        });

    /* Each test gets its own building so tests never see each other's bookings. */
    private Task<(Space, Space)> CreateTwoSpacesAsync(Action<Building>? setUp = null)
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var newBuilding = new Building(Guid.NewGuid(), $"Test {tag}");
            setUp?.Invoke(newBuilding);
            var building = await _buildings.InsertAsync(newBuilding, autoSave: true);
            var floor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            var a = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room A {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
            var b = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room B {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
            return (a, b);
        });
}
