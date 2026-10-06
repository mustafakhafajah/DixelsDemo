using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* An idempotency key belongs to the person who sent it: their retry gets their first booking back, and
 * someone else sending the same key gets a booking of their own (never the first person's). */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class IdempotencyKeyTests : PortalEntityFrameworkCoreTestBase
{
    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    private readonly IBookingAppService _service;
    private readonly ICurrentPrincipalAccessor _principal;
    private readonly IRepository<Booking, Guid> _bookings;

    public IdempotencyKeyTests()
    {
        _service = GetRequiredService<IBookingAppService>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
    }

    [Fact]
    public async Task The_same_persons_retry_gets_the_same_booking()
    {
        var (roomA, _) = await CreateTwoRoomsAsync();
        var key = Guid.NewGuid().ToString();
        var person = Guid.NewGuid();

        var first = await BookAsAsync(person, new CreateBookingDto { SpaceId = roomA.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1), IdempotencyKey = key });
        var retry = await BookAsAsync(person, new CreateBookingDto { SpaceId = roomA.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1), IdempotencyKey = key });

        retry.Id.ShouldBe(first.Id);
        (await WithUnitOfWorkAsync(() => _bookings.CountAsync(b => b.IdempotencyKey == key))).ShouldBe(1);
    }

    [Fact]
    public async Task Another_persons_identical_key_never_returns_their_booking()
    {
        var (roomA, roomB) = await CreateTwoRoomsAsync();
        var key = Guid.NewGuid().ToString();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        var alices = await BookAsAsync(alice, new CreateBookingDto { SpaceId = roomA.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1), IdempotencyKey = key });
        var bobs = await BookAsAsync(bob, new CreateBookingDto { SpaceId = roomB.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1), IdempotencyKey = key });

        bobs.Id.ShouldNotBe(alices.Id);
        bobs.OwnerUserId.ShouldBe(bob);
        bobs.SpaceId.ShouldBe(roomB.Id);
        (await WithUnitOfWorkAsync(() => _bookings.CountAsync(b => b.IdempotencyKey == key))).ShouldBe(2);
    }

    /* Two retries at the very same moment end at the database's unique index instead (PostgreSQL only); the loser
     * drops its refused insert with DeleteAsync before answering with the winner's booking. That only works if
     * deleting a booking that was never saved simply forgets it. */
    [Fact]
    public async Task Deleting_a_booking_that_was_never_saved_forgets_it()
    {
        var (roomA, _) = await CreateTwoRoomsAsync();
        var manager = GetRequiredService<BookingManager>();
        var id = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            var booking = await manager.CreateAsync(roomA.Id, Guid.NewGuid(), Ten, Ten.AddHours(1));
            id = booking.Id;
            await _bookings.InsertAsync(booking);
            await _bookings.DeleteAsync(booking);
        });

        using (GetRequiredService<IDataFilter>().Disable<ISoftDelete>())
            (await WithUnitOfWorkAsync(() => _bookings.FindAsync(id))).ShouldBeNull();
    }

    private async Task<BookingDto> BookAsAsync(Guid userId, CreateBookingDto input)
    {
        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
               {
                   new(AbpClaimTypes.UserId, userId.ToString()),
                   new(AbpClaimTypes.UserName, "u" + userId.ToString("N")[..8]),
               }))))
        {
            return await _service.CreateAsync(input);
        }
    }

    private Task<(Space, Space)> CreateTwoRoomsAsync() => WithUnitOfWorkAsync(async () =>
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(Guid.NewGuid(), $"Keys {tag}"), autoSave: true);
        var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
        var spaces = GetRequiredService<IRepository<Space, Guid>>();
        var a = await spaces.InsertAsync(new Space(Guid.NewGuid(), $"Key A {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
        var b = await spaces.InsertAsync(new Space(Guid.NewGuid(), $"Key B {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
        return (a, b);
    });
}
