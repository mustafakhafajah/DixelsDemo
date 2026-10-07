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
using Dixels.Portal.TimeZones;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Bookings;

/* People invited to a booking, through the real booking service: who sees it, the rules on who can be invited,
 * answering (accept / tentative / decline), the emails and calendar invitations each change queues, and wiping
 * guests' addresses once it is over. (Tests grant every permission, so the owner-only checks are not exercised here.) */
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

        var invited = booking.Attendees.ShouldHaveSingleItem();
        invited.UserId.ShouldBe(abed.Id);
        invited.Response.ShouldBe("none");
        booking.GuestCount.ShouldBe(1);
        booking.Guests.Select(g => g.Email).ShouldBe(["guest@outside.test"]);
        using (_principal.As(abed))
        {
            (await MyListAsync(abed)).ShouldContain(booking.Id);
            (await _service.GetBusyListAsync(new BusyListFilterDto { SpaceId = room.Id })).Items.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Everyone_invited_and_the_owner_get_a_calendar_invitation()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");

        var booking = await BookAsync(room, User(abed), Guest("guest@outside.test"));

        var toAbed = (await EmailsToAsync(abed.Email, room)).ShouldHaveSingleItem();
        toAbed.Subject.ShouldBe($"Invitation: {room.GetName()}, {Ten:ddd d MMM}");
        toAbed.Method.ShouldBe(BookingCalendar.Request);
        var calendar = Unfold(toAbed.Calendar!);
        calendar.ShouldContain($"UID:{BookingCalendar.Uid(booking.Id)}");
        calendar.ShouldContain("SEQUENCE:1");
        calendar.ShouldContain(l => l.StartsWith("ORGANIZER;CN=") && l.EndsWith($":mailto:{OwnerEmail}"));
        calendar.ShouldContain($"ATTENDEE;CN=Abed Tester;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:mailto:{abed.Email}");
        calendar.ShouldContain("ATTENDEE;CUTYPE=INDIVIDUAL;ROLE=REQ-PARTICIPANT;PARTSTAT=NEEDS-ACTION;RSVP=TRUE:mailto:guest@outside.test");

        var toGuest = (await EmailsToAsync("guest@outside.test", room)).ShouldHaveSingleItem();
        toGuest.Method.ShouldBe(BookingCalendar.Request);
        toGuest.Body.ShouldContain("Hello,"); // no name for a guest; the buttons are covered by BookingEmailsTests

        /* The owner's confirmation puts it in their calendar too. */
        (await EmailsToAsync(OwnerEmail, room)).ShouldHaveSingleItem().Method.ShouldBe(BookingCalendar.Request);
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
    public async Task Each_answer_is_recorded_and_the_owner_hears_each_change_once()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var booking = await BookAsync(room, User(abed));

        using (_principal.As(abed))
        {
            (await RespondAsync(booking.Id, "accepted")).Attendees.ShouldHaveSingleItem().Response.ShouldBe("accepted");
            await RespondAsync(booking.Id, "accepted"); // the same answer again: nothing changes, no email
            (await RespondAsync(booking.Id, "Tentative")).Attendees.ShouldHaveSingleItem().Response.ShouldBe("tentative");
            (await RespondAsync(booking.Id, "declined")).Attendees.ShouldHaveSingleItem().Response.ShouldBe("declined");
        }

        var row = await WithUnitOfWorkAsync(() => _attendees.FirstAsync(a => a.BookingId == booking.Id));
        row.Response.ShouldBe(AttendeeResponse.Declined);
        row.RespondedAt.ShouldNotBeNull();
        var day = $"{room.GetName()}, {Ten:ddd d MMM}";
        var answers = (await EmailsToAsync(OwnerEmail, room)).Where(e => e.Method == null).ToList();
        answers.Select(e => e.Subject).ShouldBe([$"Accepted: {day}", $"Maybe: {day}", $"Declined: {day}"], ignoreOrder: true);
        answers.First(e => e.Subject.StartsWith("Accepted")).Body.ShouldContain("Abed Tester has accepted your booking:");
    }

    [Fact]
    public async Task A_bad_answer_someone_not_invited_or_a_cancelled_booking_is_refused()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var mona = await CreateUserAsync("Mona");
        var booking = await BookAsync(room, User(abed));

        using (_principal.As(abed))
        {
            foreach (var bad in new[] { null, "", "none", "maybe" })
                (await Should.ThrowAsync<UserFriendlyException>(() => RespondAsync(booking.Id, bad)))
                    .Code.ShouldBe(PortalDomainErrorCodes.BookingInvalidResponse);
        }
        using (_principal.As(mona))
        {
            (await Should.ThrowAsync<UserFriendlyException>(() => RespondAsync(booking.Id, "accepted")))
                .Code.ShouldBe(PortalDomainErrorCodes.BookingNotAttendee);
        }
        await _service.CancelAsync(booking.Id);
        using (_principal.As(abed))
        {
            (await Should.ThrowAsync<UserFriendlyException>(() => RespondAsync(booking.Id, "accepted")))
                .Code.ShouldBe(PortalDomainErrorCodes.BookingLocked);
        }
    }

    [Fact]
    public async Task Declining_keeps_you_listed_frees_your_seat_and_takes_it_off_your_schedule_until_you_change_your_mind()
    {
        var room = await CreateRoomAsync(capacity: 2);
        var abed = await CreateUserAsync("Abed");
        var mona = await CreateUserAsync("Mona");
        var booking = await BookAsync(room, User(abed));

        using (_principal.As(abed))
        {
            await RespondAsync(booking.Id, "declined");
            (await MyListAsync(abed)).ShouldNotContain(booking.Id);
            (await _service.GetBusyListAsync(new BusyListFilterDto { SpaceId = room.Id })).Items.ShouldHaveSingleItem();
            (await _service.GetAsync(booking.Id)).Id.ShouldBe(booking.Id);
        }

        /* Abed no longer takes a seat, so Mona fits; saving the list again keeps Abed's answer. */
        var changed = await _service.SetAttendeesAsync(booking.Id, new SetBookingAttendeesDto { Attendees = [User(abed), User(mona)] });
        changed.Attendees.Single(a => a.UserId == abed.Id).Response.ShouldBe("declined");
        changed.Attendees.Single(a => a.UserId == mona.Id).Response.ShouldBe("none");
        (await EmailsToAsync(abed.Email, room)).ShouldHaveSingleItem(); // only the first invitation, nothing new

        using (_principal.As(abed))
        {
            await RespondAsync(booking.Id, "accepted");
            (await MyListAsync(abed)).ShouldContain(booking.Id);
        }
    }

    [Fact]
    public async Task Answering_for_the_whole_series_from_one_date_leaves_the_earlier_dates_and_is_one_email()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var series = await _service.CreateSeriesAsync(new CreateBookingSeriesDto
        {
            SpaceId = room.Id,
            Occurrences = Enumerable.Range(0, 3).Select(w => new TimeWindowDto { StartUtc = Ten.AddDays(7 * w), EndUtc = Ten.AddDays(7 * w).AddHours(1) }).ToList(),
            Attendees = [User(abed)],
        });
        var invitation = (await EmailsToAsync(abed.Email, room)).ShouldHaveSingleItem();
        invitation.Subject.ShouldBe($"Invitation: 3 bookings in {room.GetName()}");
        Unfold(invitation.Calendar!).Count(l => l == "BEGIN:VEVENT").ShouldBe(3);

        using (_principal.As(abed))
            await _service.RespondAsync(series.Created[1].Id, new RespondToBookingDto { Response = "declined", WholeSeries = true });

        var rows = await WithUnitOfWorkAsync(() => _attendees.GetListAsync(a => a.UserId == abed.Id));
        rows.Single(a => a.BookingId == series.Created[0].Id).Response.ShouldBe(AttendeeResponse.None);
        rows.Where(a => a.BookingId != series.Created[0].Id).ShouldAllBe(a => a.Response == AttendeeResponse.Declined);
        (await EmailsToAsync(OwnerEmail, room)).Where(e => e.Subject.StartsWith("Declined")).ShouldHaveSingleItem()
            .Subject.ShouldBe($"Declined: 2 bookings in {room.GetName()}");
    }

    [Fact]
    public async Task Changing_the_list_invites_the_new_people_and_tells_the_removed_ones()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var booking = await BookAsync(room, User(abed));

        var changed = await _service.SetAttendeesAsync(booking.Id, new SetBookingAttendeesDto { Attendees = [Guest("guest@outside.test")] });

        changed.Attendees.ShouldBeEmpty();
        changed.Guests.Select(g => g.Email).ShouldBe(["guest@outside.test"]);
        var toAbed = await EmailsToAsync(abed.Email, room);
        toAbed.Select(e => e.Subject)
            .ShouldBe([$"Invitation: {room.GetName()}, {Ten:ddd d MMM}", $"No longer invited: {room.GetName()}, {Ten:ddd d MMM}"], ignoreOrder: true);
        /* Taking Abed off cancels the event in Abed's calendar only: the cancellation names nobody else. */
        var off = toAbed.Single(e => e.Subject.StartsWith("No longer"));
        off.Method.ShouldBe(BookingCalendar.Cancel);
        Unfold(off.Calendar!).Where(l => l.StartsWith("ATTENDEE")).ShouldHaveSingleItem().ShouldEndWith($"mailto:{abed.Email}");
        (await EmailsToAsync("guest@outside.test", room)).ShouldHaveSingleItem().Subject.ShouldStartWith("Invitation");
    }

    [Fact]
    public async Task Moving_or_cancelling_updates_everyone_invited_and_their_calendars()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var booking = await BookAsync(room, User(abed), Guest("guest@outside.test"));

        await _service.RescheduleAsync(booking.Id, new RescheduleBookingDto { StartUtc = Ten.AddHours(2), EndUtc = Ten.AddHours(3) });
        await _service.CancelAsync(booking.Id);

        foreach (var to in new[] { OwnerEmail, abed.Email, "guest@outside.test" })
        {
            var emails = await EmailsToAsync(to, room);
            var moved = emails.Single(e => e.Subject == $"Booking changed: {room.GetName()}, {Ten:ddd d MMM}");
            moved.Method.ShouldBe(BookingCalendar.Request);
            Unfold(moved.Calendar!).ShouldContain("SEQUENCE:2");
            var cancelled = emails.Single(e => e.Subject == $"Booking cancelled: {room.GetName()}, {Ten:ddd d MMM}");
            cancelled.Method.ShouldBe(BookingCalendar.Cancel);
            Unfold(cancelled.Calendar!).ShouldContain("SEQUENCE:3");
            Unfold(cancelled.Calendar!).ShouldContain("STATUS:CANCELLED");
        }
    }

    [Fact]
    public async Task A_cancellation_message_reaches_the_owner_and_everyone_invited()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        var booking = await BookAsync(room, User(abed), Guest("guest@outside.test"));

        await _service.UpdateAsync(booking.Id, new UpdateBookingDto
        {
            Lifecycle = UpdateBookingDto.Cancelled,
            Message = new CancellationMessageDto { Subject = "Office closed", Message = "The heating is broken.\nSorry!" },
        });

        foreach (var to in new[] { OwnerEmail, abed.Email, "guest@outside.test" })
        {
            var email = (await EmailsToAsync(to, room)).Single(e => e.Method == BookingCalendar.Cancel);
            email.Subject.ShouldBe("Office closed");
            email.Body.ShouldContain("The heating is broken.<br>Sorry!");
        }
    }

    [Fact]
    public async Task Each_reader_sees_times_in_their_own_time_zone()
    {
        var room = await CreateRoomAsync();
        var abed = await CreateUserAsync("Abed");
        using (_principal.As(abed))
            await GetRequiredService<ITimeZonePreferenceAppService>().SetAsync(new TimeZonePreferenceDto { TimeZone = "Asia/Amman" });

        await BookAsync(room, User(abed), Guest("guest@outside.test"));

        /* Abed chose Amman (UTC+3); the owner chose nothing and the guest can't, so they get the building's zone (UTC). */
        (await EmailsToAsync(abed.Email, room)).ShouldHaveSingleItem().Body
            .ShouldContain($"{Ten.AddHours(3):HH:mm}–{Ten.AddHours(4):HH:mm} (UTC+03:00) Amman");
        (await EmailsToAsync(OwnerEmail, room)).ShouldHaveSingleItem().Body.ShouldContain($"{Ten:HH:mm}–{Ten.AddHours(1):HH:mm} (UTC)");
        (await EmailsToAsync("guest@outside.test", room)).ShouldHaveSingleItem().Body.ShouldContain($"{Ten:HH:mm}–{Ten.AddHours(1):HH:mm} (UTC)");
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

        (await _service.GetAsync(upcoming.Id)).Guests.Select(g => g.Email).ShouldBe(["upcoming@outside.test"]);
        foreach (var id in new[] { ended.Id, cancelled.Id })
        {
            var dto = await _service.GetAsync(id);
            dto.Guests.ShouldBeEmpty();
            dto.GuestCount.ShouldBe(1);
        }
    }

    [Fact]
    public async Task An_outside_guest_answers_through_their_private_link_and_the_owner_is_told()
    {
        var room = await CreateRoomAsync();
        var booking = await BookAsync(room, Guest("guest@outside.test"));
        var token = await IssueGuestTokenAsync(booking.Id, "guest@outside.test");
        var invitations = GetRequiredService<IBookingInvitationAppService>();

        (await invitations.GetAsync(token)).Response.ShouldBe("none");
        var answered = await invitations.RespondAsync(token, new RespondToBookingDto { Response = "tentative" });

        answered.Response.ShouldBe("tentative");
        answered.SpaceName.ShouldBe(room.GetName());
        (await _service.GetAsync(booking.Id)).Guests.ShouldHaveSingleItem().Response.ShouldBe("tentative");
        (await EmailsToAsync(OwnerEmail, room)).Select(e => e.Subject).ShouldContain($"Maybe: {room.GetName()}, {Ten:ddd d MMM}");
        /* The same answer again tells nobody twice. */
        await invitations.RespondAsync(token, new RespondToBookingDto { Response = "tentative" });
        (await EmailsToAsync(OwnerEmail, room)).Count(e => e.Subject.StartsWith("Maybe")).ShouldBe(1);
    }

    [Fact]
    public async Task A_wrong_link_or_one_for_a_booking_that_is_over_does_not_work()
    {
        var room = await CreateRoomAsync();
        var booking = await BookAsync(room, Guest("guest@outside.test"));
        var token = await IssueGuestTokenAsync(booking.Id, "guest@outside.test");
        var invitations = GetRequiredService<IBookingInvitationAppService>();

        (await Should.ThrowAsync<UserFriendlyException>(() => invitations.GetAsync("not-the-secret")))
            .Code.ShouldBe(PortalDomainErrorCodes.BookingInvitationNotFound);

        await _service.CancelAsync(booking.Id);
        (await Should.ThrowAsync<UserFriendlyException>(() => invitations.RespondAsync(token, new RespondToBookingDto { Response = "accepted" })))
            .Code.ShouldBe(PortalDomainErrorCodes.BookingLocked);

        /* Once the booking is over the guest's address and their link are wiped. */
        await GetRequiredService<BookingGuestCleaner>().ForgetFinishedAsync();
        (await Should.ThrowAsync<UserFriendlyException>(() => invitations.GetAsync(token)))
            .Code.ShouldBe(PortalDomainErrorCodes.BookingInvitationNotFound);
    }

    /* Tests have no portal address, so invitations carry no links: the guest's secret is issued here instead. */
    private Task<string> IssueGuestTokenAsync(Guid bookingId, string email)
        => WithUnitOfWorkAsync(async () =>
        {
            var row = await _attendees.FirstAsync(a => a.BookingId == bookingId && a.Email == email);
            var token = row.IssueResponseToken();
            await _attendees.UpdateAsync(row, autoSave: true);
            return token;
        });

    private static AttendeeInputDto User(IdentityUser user) => new() { UserId = user.Id };

    private static AttendeeInputDto Guest(string email) => new() { Email = email };

    private Task<BookingDto> RespondAsync(Guid bookingId, string? response)
        => _service.RespondAsync(bookingId, new RespondToBookingDto { Response = response });

    private async Task<List<Guid>> MyListAsync(IdentityUser user)
        => (await _service.GetListAsync(new BookingListFilterDto { OwnerUserId = user.Id, FromUtc = Ten.AddHours(-1), ToUtc = Ten.AddHours(2) }))
            .Items.Select(b => b.Id).ToList();

    private Task<BookingDto> BookAsync(Space room, params AttendeeInputDto[] people) => BookAsync(room, people, at: 0);

    /* at: hours after 10:00 on the test day, so several bookings in one room don't clash. */
    private Task<BookingDto> BookAsync(Space room, AttendeeInputDto person, int at) => BookAsync(room, [person], at);

    private Task<BookingDto> BookAsync(Space room, AttendeeInputDto[] people, int at)
        => _service.CreateAsync(new CreateBookingDto
        {
            SpaceId = room.Id, StartUtc = Ten.AddHours(at), EndUtc = Ten.AddHours(at + 1), Attendees = people.ToList(),
        });

    private async Task<List<QueuedEmail>> EmailsToAsync(string to, Space room)
        => (await WithUnitOfWorkAsync(() => QueuedEmails.ReadAsync(
                GetRequiredService<IBackgroundJobRepository>(), GetRequiredService<IBackgroundJobSerializer>())))
            .Where(e => e.To == to && e.Body.Contains(room.GetName())).ToList();

    /* The calendar's lines, with folded continuations joined back on. */
    private static List<string> Unfold(string ics) => ics.Replace("\r\n ", "").Split("\r\n").ToList();

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
