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

    private Task BookAsync(Space space, Guid ownerId, double fromHours, double toHours)
        => WithUnitOfWorkAsync(async () =>
        {
            var booking = await _manager.CreateAsync(space.Id, ownerId, Ten.AddHours(fromHours), Ten.AddHours(toHours));
            await _bookings.InsertAsync(booking, autoSave: true);
        });

    /* Each test gets its own building so tests never see each other's bookings. */
    private Task<(Space, Space)> CreateTwoSpacesAsync()
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Test {tag}"), autoSave: true);
            var floor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            var a = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room A {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
            var b = await _spaces.InsertAsync(new Space(Guid.NewGuid(), $"Room B {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
            return (a, b);
        });
}
