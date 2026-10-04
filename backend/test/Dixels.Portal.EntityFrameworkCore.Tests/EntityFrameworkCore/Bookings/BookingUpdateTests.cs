using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* PATCH /api/app/bookings/{id} picks the change from its body (a new time, "cancelled" or "ended"), and
 * PATCH /api/app/booking-series/{id} cancels a series from a given moment on. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BookingUpdateTests : PortalEntityFrameworkCoreTestBase
{
    private readonly BookingManager _manager;
    private readonly IBookingAppService _service;
    private readonly IRepository<Booking, Guid> _bookings;

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    public BookingUpdateTests()
    {
        _manager = GetRequiredService<BookingManager>();
        _service = GetRequiredService<IBookingAppService>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
    }

    [Fact]
    public async Task A_new_time_moves_the_booking()
    {
        var booking = await BookAsync(await CreateRoomAsync(), 0, 1);

        var moved = await _service.UpdateAsync(booking.Id, new UpdateBookingDto
        {
            StartUtc = Ten.AddHours(2), EndUtc = Ten.AddHours(3), ExpectedVersion = booking.Version,
        });

        moved.StartUtc.ShouldBe(Ten.AddHours(2));
        moved.EndUtc.ShouldBe(Ten.AddHours(3));
        moved.Version.ShouldBe(booking.Version + 1);
    }

    [Fact]
    public async Task Lifecycle_cancelled_cancels_the_booking_and_keeps_it()
    {
        var booking = await BookAsync(await CreateRoomAsync(), 0, 1);

        var cancelled = await _service.UpdateAsync(booking.Id, new UpdateBookingDto { Lifecycle = UpdateBookingDto.Cancelled });

        cancelled.Lifecycle.ShouldBe("cancelled");
        (await WithUnitOfWorkAsync(() => _bookings.GetAsync(booking.Id))).Status.ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task Lifecycle_ended_ends_a_booking_in_progress_now()
    {
        var booking = await BookAsync(await CreateRoomAsync(), 0, 1);
        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _bookings.GetAsync(booking.Id);
            b.StartUtc = DateTime.UtcNow.AddMinutes(-30);
            b.EndUtc = DateTime.UtcNow.AddMinutes(30);
            await _bookings.UpdateAsync(b, autoSave: true);
        });

        var ended = await _service.UpdateAsync(booking.Id, new UpdateBookingDto { Lifecycle = UpdateBookingDto.Ended });

        ended.EndUtc.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        ended.Lifecycle.ShouldBe("ended");
    }

    [Theory]
    [InlineData("deleted", false, false)] /* not a lifecycle a booking can be moved to */
    [InlineData("cancelled", true, true)] /* a time and a lifecycle at once */
    [InlineData(null, true, false)]       /* a new time needs both ends */
    [InlineData(null, false, false)]      /* nothing to change */
    public void A_body_that_says_nothing_clear_is_refused(string? lifecycle, bool start, bool end)
    {
        var body = new UpdateBookingDto
        {
            Lifecycle = lifecycle,
            StartUtc = start ? Ten : null,
            EndUtc = end ? Ten.AddHours(1) : null,
        };

        body.Validate(new ValidationContext(body)).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("cancelled", false)]
    [InlineData("ended", false)]
    [InlineData(null, true)]
    public void A_clear_body_is_accepted(string? lifecycle, bool time)
    {
        var body = new UpdateBookingDto
        {
            Lifecycle = lifecycle,
            StartUtc = time ? Ten : null,
            EndUtc = time ? Ten.AddHours(1) : null,
        };

        body.Validate(new ValidationContext(body)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_series_is_cancelled_from_the_given_moment_on()
    {
        var room = await CreateRoomAsync();
        var seriesId = Guid.NewGuid();
        var first = await BookAsync(room, 0, 1, seriesId);
        var second = await BookAsync(room, 24, 25, seriesId);
        var third = await BookAsync(room, 48, 49, seriesId);

        var result = await _service.CancelSeriesAsync(seriesId, new CancelBookingSeriesDto
        {
            Lifecycle = CancellationDto.Cancelled, FromUtc = second.StartUtc,
        });

        result.CancelledCount.ShouldBe(2);
        result.SeriesId.ShouldBe(seriesId);
        var statuses = await WithUnitOfWorkAsync(async () => (await _bookings.GetListAsync(b => b.SeriesId == seriesId))
            .ToDictionary(b => b.Id, b => b.Status));
        statuses[first.Id].ShouldBe(BookingStatus.Confirmed);
        statuses[second.Id].ShouldBe(BookingStatus.Cancelled);
        statuses[third.Id].ShouldBe(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task An_unknown_series_is_not_found()
    {
        var error = await Should.ThrowAsync<UserFriendlyException>(() => _service.CancelSeriesAsync(Guid.NewGuid(),
            new CancelBookingSeriesDto { Lifecycle = CancellationDto.Cancelled, FromUtc = Ten }));

        error.Code.ShouldBe(PortalDomainErrorCodes.BookingNotFound);
    }

    private Task<Booking> BookAsync(Space space, double fromHours, double toHours, Guid? seriesId = null)
        => WithUnitOfWorkAsync(async () =>
        {
            var booking = await _manager.CreateAsync(space.Id, Guid.NewGuid(), Ten.AddHours(fromHours), Ten.AddHours(toHours), seriesId: seriesId);
            return await _bookings.InsertAsync(booking, autoSave: true);
        });

    private Task<Space> CreateRoomAsync()
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(Guid.NewGuid(), $"Patch {tag}"), autoSave: true);
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            return await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(
                new Space(Guid.NewGuid(), $"Room {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
        });
}
