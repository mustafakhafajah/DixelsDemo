using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Notifications;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Notifications;

/* Through the real booking service: each change queues the right email for the person who booked, and a reminder
 * job 10 minutes before the start. Tests don't run background jobs, so the queued jobs are read from the job table. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BookingEmailTests : PortalEntityFrameworkCoreTestBase
{
    /* The signed-in test user (FakeCurrentPrincipalAccessor) books; it needs a real account with an email address. */
    private static readonly Guid OwnerId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");
    private const string OwnerEmail = "booking-owner@dixels.test";

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    private readonly IBookingAppService _bookingService;

    public BookingEmailTests()
    {
        _bookingService = GetRequiredService<IBookingAppService>();
    }

    [Fact]
    public async Task A_booking_queues_a_confirmation_and_a_reminder_10_minutes_before()
    {
        var room = await CreateRoomAsync();

        var booking = await _bookingService.CreateAsync(new CreateBookingDto { SpaceId = room.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1) });

        (await EmailsAboutAsync(room)).Select(e => e.Subject).ShouldBe([$"Booking confirmed: {room.GetName()}, {Ten:ddd d MMM}"]);
        var reminder = (await RemindersForAsync(booking.Id)).ShouldHaveSingleItem();
        reminder.Args.Version.ShouldBe(1);
        reminder.NextTryTime.ShouldBe(Ten.AddMinutes(-10), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task A_series_is_one_confirmation_and_a_reminder_per_date()
    {
        var room = await CreateRoomAsync();

        var result = await _bookingService.CreateSeriesAsync(new CreateBookingSeriesDto
        {
            SpaceId = room.Id,
            Occurrences = Enumerable.Range(0, 3).Select(w => new TimeWindowDto { StartUtc = Ten.AddDays(7 * w), EndUtc = Ten.AddDays(7 * w).AddHours(1) }).ToList(),
        });

        (await EmailsAboutAsync(room)).ShouldHaveSingleItem().Subject.ShouldBe($"3 bookings confirmed: {room.GetName()}");
        foreach (var b in result.Created) (await RemindersForAsync(b.Id)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Rescheduling_emails_the_new_time_and_queues_a_reminder_for_it()
    {
        var room = await CreateRoomAsync();
        var booking = await _bookingService.CreateAsync(new CreateBookingDto { SpaceId = room.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1) });

        await _bookingService.RescheduleAsync(booking.Id, new RescheduleBookingDto { StartUtc = Ten.AddHours(2), EndUtc = Ten.AddHours(3) });

        (await EmailsAboutAsync(room)).Select(e => e.Subject).ShouldContain($"Booking changed: {room.GetName()}, {Ten:ddd d MMM}");
        /* The first reminder still sits in the queue but carries the old version, so it will do nothing. */
        (await RemindersForAsync(booking.Id)).Select(r => r.Args.Version).OrderBy(v => v).ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Cancelling_emails_the_owner_once()
    {
        var room = await CreateRoomAsync();
        var booking = await _bookingService.CreateAsync(new CreateBookingDto { SpaceId = room.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1) });

        await _bookingService.CancelAsync(booking.Id);
        await _bookingService.CancelAsync(booking.Id); // already cancelled: nothing changes, no second email

        (await EmailsAboutAsync(room)).Count(e => e.Subject.StartsWith("Booking cancelled")).ShouldBe(1);
    }

    [Fact]
    public async Task Cancelling_a_series_is_one_email_listing_every_date()
    {
        var room = await CreateRoomAsync();
        var result = await _bookingService.CreateSeriesAsync(new CreateBookingSeriesDto
        {
            SpaceId = room.Id,
            Occurrences = Enumerable.Range(0, 3).Select(w => new TimeWindowDto { StartUtc = Ten.AddDays(7 * w), EndUtc = Ten.AddDays(7 * w).AddHours(1) }).ToList(),
        });

        await _bookingService.CancelSeriesAsync(result.SeriesId!.Value, new CancelBookingSeriesDto { Lifecycle = CancellationDto.Cancelled, FromUtc = Ten });

        (await EmailsAboutAsync(room)).Where(e => e.Subject.Contains("cancelled")).ShouldHaveSingleItem()
            .Subject.ShouldBe("3 bookings cancelled");
    }

    [Fact]
    public async Task A_booking_starting_within_10_minutes_gets_no_reminder()
    {
        var room = await CreateRoomAsync();
        var booking = await _bookingService.CreateAsync(new CreateBookingDto { SpaceId = room.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1) });
        var soon = await WithUnitOfWorkAsync(async () =>
        {
            var b = await GetRequiredService<IRepository<Booking, Guid>>().GetAsync(booking.Id);
            b.StartUtc = DateTime.UtcNow.AddMinutes(5);
            b.Version = 99;
            return b;
        });

        await WithUnitOfWorkAsync(() => GetRequiredService<BookingNotifier>().BookingsCreatedAsync([soon]));

        (await RemindersForAsync(booking.Id)).ShouldNotContain(r => r.Args.Version == 99);
    }

    [Fact]
    public async Task The_reminder_job_sends_only_while_the_booking_is_unchanged()
    {
        var room = await CreateRoomAsync();
        var booking = await _bookingService.CreateAsync(new CreateBookingDto { SpaceId = room.Id, StartUtc = Ten, EndUtc = Ten.AddHours(1) });
        var job = GetRequiredService<BookingReminderJob>();

        await job.ExecuteAsync(new BookingReminderJobArgs { BookingId = booking.Id, Version = 1 });
        (await EmailsAboutAsync(room)).Count(e => e.Subject.StartsWith("Starting soon")).ShouldBe(1);

        await _bookingService.CancelAsync(booking.Id);
        await job.ExecuteAsync(new BookingReminderJobArgs { BookingId = booking.Id, Version = 1 });
        (await EmailsAboutAsync(room)).Count(e => e.Subject.StartsWith("Starting soon")).ShouldBe(1);
    }

    private async Task<List<BackgroundEmailSendingJobArgs>> EmailsAboutAsync(Space room)
        => (await JobsAsync<BackgroundEmailSendingJobArgs>())
            .Select(j => j.Args).Where(a => a.To == OwnerEmail && a.Body.Contains(room.GetName())).ToList();

    private async Task<List<(BookingReminderJobArgs Args, DateTime NextTryTime)>> RemindersForAsync(Guid bookingId)
        => (await JobsAsync<BookingReminderJobArgs>()).Where(j => j.Args.BookingId == bookingId).ToList();

    private Task<List<(T Args, DateTime NextTryTime)>> JobsAsync<T>()
        => WithUnitOfWorkAsync(async () =>
        {
            var name = BackgroundJobNameAttribute.GetName<T>();
            var serializer = GetRequiredService<IBackgroundJobSerializer>();
            var jobs = await GetRequiredService<IBackgroundJobRepository>().GetListAsync();
            return jobs.Where(j => j.JobName == name)
                .Select(j => ((T)serializer.Deserialize(j.JobArgs, typeof(T)), j.NextTryTime)).ToList();
        });

    private Task<Space> CreateRoomAsync()
        => WithUnitOfWorkAsync(async () =>
        {
            await EnsureOwnerAccountAsync();
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(Guid.NewGuid(), $"Mail {tag}"), autoSave: true);
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            return await GetRequiredService<IRepository<Space, Guid>>()
                .InsertAsync(new Space(Guid.NewGuid(), $"Room {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom), autoSave: true);
        });

    private async Task EnsureOwnerAccountAsync()
    {
        var users = GetRequiredService<IdentityUserManager>();
        if (await users.FindByIdAsync(OwnerId.ToString()) != null) return;
        (await users.CreateAsync(new IdentityUser(OwnerId, "booking-owner", OwnerEmail) { Name = "Sara" })).Succeeded.ShouldBeTrue();
    }
}
