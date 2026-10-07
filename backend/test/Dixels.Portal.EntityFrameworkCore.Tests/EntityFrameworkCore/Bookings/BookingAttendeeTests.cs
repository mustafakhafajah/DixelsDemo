using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Buildings;
using Dixels.Portal.EntityFrameworkCore.Profiles;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* People invited to a booking, through the real booking service: who sees it, the rules on who can be invited,
 * leaving it, the emails each change queues, and wiping guests' addresses once it is over.
 * (Tests grant every permission, so the owner-only checks are not exercised here.) */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class BookingAttendeeTests : PortalEntityFrameworkCoreTestBase
{
    /* The signed-in test user (FakeCurrentPrincipalAccessor) books. */
    private static readonly Guid OwnerId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");
    private const string OwnerEmail = "booking-owner@dixels.test";

    private static readonly DateTime Ten = DateTime.UtcNow.Date.AddDays(30).AddHours(10);

    private readonly IBookingAppService _service;
    private readonly ICurrentPrincipalAccessor _principal;
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<BookingAttendee, Guid> _attendees;

    public BookingAttendeeTests()
    {
        _service = GetRequiredService<IBookingAppService>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
        _bookings = GetRequiredService<IRepository<Booking, Guid>>();
        _attendees = GetRequiredService<IRepository<BookingAttendee, Guid>>();
    }

    [Fact]
    public async Task An_invited_user_has_the_booking_on_their_list_and_not_as_busy()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");

        var booking = await BookAsync(room, User(abed), Guest("Guest@Outside.test"));

        booking.Attendees.ShouldHaveSingleItem().UserId.ShouldBe(abed.Id);
        booking.GuestCount.ShouldBe(1);
        booking.Guests.ShouldBe(["guest@outside.test"]);
        using (_principal.As(abed))
        {
            var mine = await _service.GetListAsync(new BookingListFilterDto { OwnerUserId = abed.Id, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(2) });
            mine.Items.Select(b => b.Id).ShouldContain(booking.Id);
            (await _service.GetBusyListAsync(new BusyListFilterDto { SpaceId = room.Id })).Items.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Everyone_invited_gets_an_invitation()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");

        await BookAsync(room, User(abed), Guest("guest@outside.test"));

        (await EmailsToAsync(abed.Email, room)).ShouldHaveSingleItem().Subject.ShouldBe($"Invitation: {room.GetName()}, {Ten:ddd d MMM}");
        (await EmailsToAsync("guest@outside.test", room)).ShouldHaveSingleItem().Body.ShouldNotContain("Refuse");
    }

    [Fact]
    public async Task An_email_address_of_a_user_invites_that_user()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");

        var booking = await BookAsync(room, Guest(abed.Email.ToUpperInvariant()));

        booking.Attendees.ShouldHaveSingleItem().UserId.ShouldBe(abed.Id);
        booking.GuestCount.ShouldBe(0);
    }

    [Fact]
    public async Task More_people_than_the_room_seats_is_refused()
    {
        var room = await CreateRoomAsync(capacity: 2);
        var abed = await CreateUserAsync("Abed");

        var ex = await Should.ThrowAsync<UserFriendlyException>(() => BookAsync(room, User(abed), Guest("guest@outside.test")));

        ex.Code.ShouldBe(PortalDomainErrorCodes.BookingOverCapacity);
        (await WithUnitOfWorkAsync(() => _bookings.CountAsync(b => b.SpaceId == room.Id))).ShouldBe(0);
    }

    [Fact]
    public async Task The_owner_a_bad_address_or_someone_twice_is_refused()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");

        foreach (var people in new[]
                 {
                     new[] { new AttendeeInputDto { UserId = OwnerId } },
                     new[] { Guest("not-an-address") },
                     new[] { User(abed), Guest(abed.Email) },
                     new[] { new AttendeeInputDto() },
                 })
        {
            (await Should.ThrowAsync<UserFriendlyException>(() => BookAsync(room, people)))
                .Code.ShouldBe(PortalDomainErrorCodes.BookingInvalidAttendee);
        }
    }

    [Fact]
    public async Task Refusing_takes_you_off_and_tells_the_owner()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var booking = await BookAsync(room, User(abed));

        using (_principal.As(abed))
        {
            await _service.LeaveAsync(booking.Id);
            (await Should.ThrowAsync<UserFriendlyException>(() => _service.LeaveAsync(booking.Id)))
                .Code.ShouldBe(PortalDomainErrorCodes.BookingNotAttendee);
        }

        (await _service.GetAsync(booking.Id)).Attendees.ShouldBeEmpty();
        (await EmailsToAsync(OwnerEmail, room)).Select(e => e.Subject)
            .ShouldContain($"Abed Tester can't make it: {room.GetName()}, {Ten:ddd d MMM}");
    }

    [Fact]
    public async Task Refusing_a_series_from_one_date_leaves_the_earlier_dates()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var series = await _service.CreateSeriesAsync(new CreateBookingSeriesDto
        {
            SpaceId = room.Id,
            Occurrences = Enumerable.Range(0, 3).Select(w => new TimeWindowDto { StartUtc = Ten.AddDays(7 * w), EndUtc = Ten.AddDays(7 * w).AddHours(1) }).ToList(),
            Attendees = [User(abed)],
        });
        (await EmailsToAsync(abed.Email, room)).ShouldHaveSingleItem().Subject.ShouldBe($"Invitation: 3 bookings in {room.GetName()}");

        using (_principal.As(abed)) await _service.LeaveAsync(series.Created[1].Id, wholeSeries: true);

        var still = await WithUnitOfWorkAsync(() => _attendees.GetListAsync(a => a.UserId == abed.Id));
        still.Select(a => a.BookingId).ShouldBe([series.Created[0].Id]);
    }

    [Fact]
    public async Task Changing_the_list_invites_the_new_people_and_tells_the_removed_ones()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var booking = await BookAsync(room, User(abed));

        var changed = await _service.SetAttendeesAsync(booking.Id, new SetBookingAttendeesDto { Attendees = [Guest("guest@outside.test")] });

        changed.Attendees.ShouldBeEmpty();
        changed.Guests.ShouldBe(["guest@outside.test"]);
        (await EmailsToAsync(abed.Email, room)).Select(e => e.Subject)
            .ShouldBe([$"Invitation: {room.GetName()}, {Ten:ddd d MMM}", $"No longer invited: {room.GetName()}, {Ten:ddd d MMM}"], ignoreOrder: true);
        (await EmailsToAsync("guest@outside.test", room)).ShouldHaveSingleItem().Subject.ShouldStartWith("Invitation");
    }

    [Fact]
    public async Task Moving_or_cancelling_tells_everyone_invited()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var booking = await BookAsync(room, User(abed), Guest("guest@outside.test"));

        await _service.RescheduleAsync(booking.Id, new RescheduleBookingDto { StartUtc = Ten.AddHours(2), EndUtc = Ten.AddHours(3) });
        await _service.CancelAsync(booking.Id);

        foreach (var to in new[] { abed.Email, "guest@outside.test" })
        {
            var subjects = (await EmailsToAsync(to, room)).Select(e => e.Subject).ToList();
            subjects.ShouldContain($"Booking changed: {room.GetName()}, {Ten:ddd d MMM}");
            subjects.ShouldContain($"Booking cancelled: {room.GetName()}, {Ten:ddd d MMM}");
        }
    }

    [Fact]
    public async Task Guest_addresses_are_wiped_once_the_booking_is_over_and_the_count_stays()
    {
        var room = await CreateRoomAsync();
        var upcoming = await BookAsync(room, Guest("upcoming@outside.test"));
        var ended = await BookAsync(room, Guest("ended@outside.test"), at: 2);
        var cancelled = await BookAsync(room, Guest("cancelled@outside.test"), at: 4);
        await _service.CancelAsync(cancelled.Id);
        await WithUnitOfWorkAsync(async () =>
        {
            var b = await _bookings.GetAsync(ended.Id);
            b.StartUtc = DateTime.UtcNow.AddHours(-3);
            b.EndUtc = DateTime.UtcNow.AddHours(-2);
            await _bookings.UpdateAsync(b, autoSave: true);
        });

        (await GetRequiredService<BookingGuestCleaner>().ForgetFinishedAsync()).ShouldBeGreaterThanOrEqualTo(2);

        (await _service.GetAsync(upcoming.Id)).Guests.ShouldBe(["upcoming@outside.test"]);
        foreach (var id in new[] { ended.Id, cancelled.Id })
        {
            var dto = await _service.GetAsync(id);
            dto.Guests.ShouldBeEmpty();
            dto.GuestCount.ShouldBe(1);
        }
    }

    private static AttendeeInputDto User(IdentityUser user) => new() { UserId = user.Id };

    private static AttendeeInputDto Guest(string email) => new() { Email = email };

    private Task<BookingDto> BookAsync(Space room, params AttendeeInputDto[] people) => BookAsync(room, people, at: 0);

    /* at: hours after 10:00 on the test day, so several bookings in one room don't clash. */
    private Task<BookingDto> BookAsync(Space room, AttendeeInputDto person, int at) => BookAsync(room, [person], at);

    private Task<BookingDto> BookAsync(Space room, AttendeeInputDto[] people, int at)
        => _service.CreateAsync(new CreateBookingDto
        {
            SpaceId = room.Id, StartUtc = Ten.AddHours(at), EndUtc = Ten.AddHours(at + 1), Attendees = people.ToList(),
        });

    private async Task<List<BackgroundEmailSendingJobArgs>> EmailsToAsync(string to, Space room)
        => (await WithUnitOfWorkAsync(async () =>
        {
            var name = BackgroundJobNameAttribute.GetName<BackgroundEmailSendingJobArgs>();
            var serializer = GetRequiredService<IBackgroundJobSerializer>();
            var jobs = await GetRequiredService<IBackgroundJobRepository>().GetListAsync();
            return jobs.Where(j => j.JobName == name)
                .Select(j => (BackgroundEmailSendingJobArgs)serializer.Deserialize(j.JobArgs, typeof(BackgroundEmailSendingJobArgs)))
                .ToList();
        })).Where(a => a.To == to && a.Body.Contains(room.GetName())).ToList();

    private Task<IdentityUser> CreateUserAsync(string name)
        => WithUnitOfWorkAsync(async () =>
        {
            var tag = Guid.NewGuid().ToString("N")[..8];
            var user = new IdentityUser(Guid.NewGuid(), $"{name.ToLowerInvariant()}-{tag}", $"{name.ToLowerInvariant()}-{tag}@dixels.test")
                { Name = name, Surname = "Tester" };
            (await GetRequiredService<IdentityUserManager>().CreateAsync(user)).Succeeded.ShouldBeTrue();
            return user;
        });

    private Task<Space> CreateRoomAsync(int capacity = 0)
        => WithUnitOfWorkAsync(async () =>
        {
            var users = GetRequiredService<IdentityUserManager>();
            if (await users.FindByIdAsync(OwnerId.ToString()) == null)
                (await users.CreateAsync(new IdentityUser(OwnerId, "booking-owner", OwnerEmail) { Name = "Sara" })).Succeeded.ShouldBeTrue();
            var tag = Guid.NewGuid().ToString("N")[..8];
            var building = await GetRequiredService<IRepository<Building, Guid>>().InsertAsync(new Building(Guid.NewGuid(), $"Invite {tag}"), autoSave: true);
            var floor = await GetRequiredService<IRepository<Floor, Guid>>().InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            return await GetRequiredService<IRepository<Space, Guid>>().InsertAsync(
                new Space(Guid.NewGuid(), $"Room {tag}", building.Id, floor.Id, DefaultSpaceTypes.MeetingRoom) { Capacity = capacity }, autoSave: true);
        });
}
