using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Estate;
using Dixels.Portal.Settings;
using Dixels.Portal.Spaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.Users;

namespace Dixels.Portal.Bookings;

/* Emails the person who booked when their booking is made, changed or cancelled, and schedules the reminder
 * 10 minutes before it starts. The people invited hear about the same changes, plus being added or taken off;
 * the owner hears each time one of them answers. Called by the booking and blocked-time services right after they save.
 *
 * Emails that change what is in someone's calendar (confirmation, invitation, new time, cancellation, taken off the
 * list) carry a calendar invitation or cancellation (BookingCalendar), so Outlook, Gmail and phones keep the booking
 * in the calendar; those go out through BookingCalendarEmailJob. Reminders and answers are plain emails
 * (IEmailSender.QueueAsync). Either way they are queued, so they go out in the background and only if the booking
 * change is saved. A problem building an email is logged and never stops the booking itself.
 *
 * Times are written in each reader's own time zone (their Portal.TimeZone setting), or the building's when they have
 * none or are a guest. Several bookings for one person (a repeating series, or a bulk cancel) become one email
 * listing them all. */
public class BookingNotifier : ITransientDependency
{
    public static readonly TimeSpan ReminderLead = TimeSpan.FromMinutes(10);

    private readonly IEmailSender _emailSender;
    private readonly IIdentityUserRepository _users;
    private readonly IRepository<BookingAttendee, Guid> _attendees;
    private readonly BookingManager _bookingManager;
    private readonly IBackgroundJobManager _jobs;
    private readonly IAppUrlProvider _urls;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly ISettingManager _settings;

    public ILogger<BookingNotifier> Logger { get; set; } = NullLogger<BookingNotifier>.Instance;

    public BookingNotifier(IEmailSender emailSender, IIdentityUserRepository users, IRepository<BookingAttendee, Guid> attendees,
        BookingManager bookingManager, IBackgroundJobManager jobs, IAppUrlProvider urls, ICurrentUser currentUser, IClock clock,
        ISettingManager settings)
    {
        _emailSender = emailSender;
        _users = users;
        _attendees = attendees;
        _bookingManager = bookingManager;
        _jobs = jobs;
        _urls = urls;
        _currentUser = currentUser;
        _clock = clock;
        _settings = settings;
    }

    public async Task BookingsCreatedAsync(IReadOnlyCollection<Booking> bookings)
    {
        if (bookings.Count == 0) return;
        foreach (var booking in bookings) await ScheduleReminderAsync(booking);
        var s = await LoadAsync(bookings);
        if (s == null) return;
        await PerOwnerAsync(s, bookings, (owner, lines) => BookingEmails.Confirmed(FirstName(owner), lines, s.BookingsUrl), BookingCalendar.Request);
        await InviteAsync(s, bookings, s.Attendees);
    }

    public async Task BookingRescheduledAsync(Booking booking, DateTime oldStartUtc, DateTime oldEndUtc)
    {
        /* The reminder already queued for the old time no longer matches the booking's version and does nothing. */
        await ScheduleReminderAsync(booking);
        var s = await LoadAsync([booking]);
        if (s == null) return;
        /* The calendar update carries the booking's new version, so it replaces the event already in each calendar. */
        await PerOwnerAsync(s, [booking], (owner, lines) =>
            BookingEmails.Rescheduled(FirstName(owner), lines[0] with { StartUtc = oldStartUtc, EndUtc = oldEndUtc }, lines[0], s.BookingsUrl),
            BookingCalendar.Request);
        /* Guests can't sign in, so only portal users get the link. */
        await PerAttendeeAsync(s, [booking], s.Attendees, (name, ownerName, lines, first) =>
            BookingEmails.InviteRescheduled(name, ownerName, lines[0] with { StartUtc = oldStartUtc, EndUtc = oldEndUtc }, lines[0],
                first.IsGuest ? null : BookingLink(s, booking.Id)), BookingCalendar.Request);
    }

    /* reason: shown in the email, e.g. "the space is blocked for maintenance". Someone cancelling another person's
     * booking (an admin) is mentioned as such; cancelling your own is not. message: the canceller's own subject and
     * note, the same for the owner and everyone invited. */
    public async Task BookingsCancelledAsync(IReadOnlyCollection<Booking> bookings, string? reason = null, CancellationMessageDto? message = null)
    {
        if (bookings.Count == 0) return;
        var s = await LoadAsync(bookings);
        if (s == null) return;
        var (subject, note) = (message?.Subject, message?.Message);
        await PerOwnerAsync(s, bookings, (owner, lines) =>
            BookingEmails.Cancelled(FirstName(owner), lines, cancelledByOther: _currentUser.Id != owner.Id, reason, s.BookingsUrl, subject, note),
            BookingCalendar.Cancel);
        await PerAttendeeAsync(s, bookings, s.Attendees, (name, ownerName, lines, _) =>
            BookingEmails.InviteCancelled(name, ownerName, lines, reason, subject, note), BookingCalendar.Cancel);
    }

    public async Task SendReminderAsync(Booking booking)
    {
        var s = await LoadAsync([booking]);
        if (s == null) return;
        await PerOwnerAsync(s, [booking], (owner, lines) => BookingEmails.Reminder(FirstName(owner), lines[0], s.BookingsUrl), method: null);
    }

    /* The owner changed who is invited: the new people get an invitation, the ones taken off are told (with a
     * calendar cancellation that names only them, so the event leaves their own calendar). */
    public async Task AttendeesChangedAsync(Booking booking, IReadOnlyCollection<BookingAttendee> added, IReadOnlyCollection<BookingAttendee> removed)
    {
        if (added.Count == 0 && removed.Count == 0) return;
        var s = await LoadAsync([booking], removed.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value));
        if (s == null) return;
        await InviteAsync(s, [booking], added);
        await PerAttendeeAsync(s, [booking], removed, (name, ownerName, lines, _) => BookingEmails.Uninvited(name, ownerName, lines),
            BookingCalendar.Cancel, onlyThem: true);
    }

    /* Someone answered (or changed their answer): one email to the owner listing the dates it applies to. */
    public async Task AttendeeRespondedAsync(IReadOnlyCollection<Booking> bookings, Guid attendeeUserId, AttendeeResponse response)
    {
        if (bookings.Count == 0) return;
        var s = await LoadAsync(bookings, [attendeeUserId]);
        if (s == null) return;
        var attendeeName = s.Users.GetValueOrDefault(attendeeUserId)?.GetDisplayName() ?? "Someone";
        await PerOwnerAsync(s, bookings, (owner, lines) =>
            BookingEmails.Responded(FirstName(owner), attendeeName, response, lines, s.BookingsUrl), method: null);
    }

    /* Portal users get Accept / Tentative / Decline links to the first of their dates; with several dates (a repeating
     * booking) the answer covers them all, as answering a Teams series does. Guests answer from the calendar
     * invitation instead. */
    private Task InviteAsync(Snapshot s, IReadOnlyCollection<Booking> bookings, IReadOnlyCollection<BookingAttendee> attendees)
        => PerAttendeeAsync(s, bookings, attendees, (name, ownerName, lines, first) =>
            BookingEmails.Invited(name, ownerName, lines,
                first.IsGuest || s.BookingsUrl == null ? null : ResponseLinks.For(s.BookingsUrl, first.BookingId, wholeSeries: lines.Count > 1)),
            BookingCalendar.Request);

    /* Only bookings made at least 10 minutes ahead get one; the confirmation covers the rest. */
    private async Task ScheduleReminderAsync(Booking booking)
    {
        var delay = booking.StartUtc - ReminderLead - _clock.Now;
        if (delay <= TimeSpan.Zero) return;
        try
        {
            await _jobs.EnqueueAsync(new BookingReminderJobArgs { BookingId = booking.Id, Version = booking.Version },
                BackgroundJobPriority.Normal, delay);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not schedule the reminder for booking {BookingId}.", booking.Id);
        }
    }

    /* One email per owner, listing their given bookings. method: BookingCalendar.Request / Cancel to add the
     * calendar part, null for a plain email. */
    private async Task PerOwnerAsync(Snapshot s, IReadOnlyCollection<Booking> bookings,
        Func<IdentityUser, IReadOnlyList<BookingEmailLine>, EmailContent> compose, string? method)
    {
        foreach (var group in bookings.GroupBy(b => b.OwnerUserId))
        {
            try
            {
                if (!s.Users.TryGetValue(group.Key, out var owner) || string.IsNullOrWhiteSpace(owner.Email)) continue;
                var mine = group.OrderBy(b => b.StartUtc).ToList();
                var zone = await ZoneOfAsync(s, owner.Id);
                var email = compose(owner, mine.Select(b => InZone(s.Lines[b.Id], zone)).ToList());
                await SendAsync(owner.Email, email, method == null ? null : Calendar(s, method, mine, only: null, withLink: true), method);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not queue the booking email for owner {OwnerId}.", group.Key);
            }
        }
    }

    /* One email per person invited, listing the given bookings they are on. compose gets their first name (null for
     * a guest), the owner's name, the dates, and their row on the first date. A guest whose address was already
     * forgotten is skipped. onlyThem: the calendar part names only this person (taking them off), not everyone. */
    private async Task PerAttendeeAsync(Snapshot s, IReadOnlyCollection<Booking> bookings, IReadOnlyCollection<BookingAttendee> attendees,
        Func<string?, string, IReadOnlyList<BookingEmailLine>, BookingAttendee, EmailContent> compose, string? method, bool onlyThem = false)
    {
        var byId = bookings.ToDictionary(b => b.Id);
        foreach (var group in attendees.Where(a => byId.ContainsKey(a.BookingId)).GroupBy(a => (a.UserId, a.Email)))
        {
            try
            {
                var user = group.Key.UserId.HasValue ? s.Users.GetValueOrDefault(group.Key.UserId.Value) : null;
                var address = group.Key.UserId.HasValue ? user?.Email : group.Key.Email;
                if (string.IsNullOrWhiteSpace(address)) continue;
                var mine = group.Select(a => byId[a.BookingId]).OrderBy(b => b.StartUtc).ToList();
                var ownerName = s.Users.GetValueOrDefault(mine[0].OwnerUserId)?.GetDisplayName() ?? "Someone";
                var first = group.First(a => a.BookingId == mine[0].Id);
                var zone = await ZoneOfAsync(s, group.Key.UserId);
                var email = compose(user != null ? FirstName(user) : null, ownerName, mine.Select(b => InZone(s.Lines[b.Id], zone)).ToList(), first);
                var calendar = method == null ? null : Calendar(s, method, mine, onlyThem ? group.ToList() : null, withLink: user != null);
                await SendAsync(address, email, calendar, method);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Could not queue the attendee email for {Count} booking(s).", bookings.Count);
            }
        }
    }

    /* With a calendar part: our own queued job (ABP's queued email can't carry one). Without: ABP's queued email. */
    private async Task SendAsync(string to, EmailContent email, string? calendar, string? method)
    {
        if (calendar == null || method == null)
        {
            await _emailSender.QueueAsync(to, email.Subject, email.Html);
            return;
        }
        await _jobs.EnqueueAsync(new BookingCalendarEmailArgs
        {
            To = to, Subject = email.Subject, Html = email.Html, Text = email.Text, Calendar = calendar, Method = method,
        });
    }

    /* The bookings as calendar events: the owner organises, everyone invited (or only the given rows) attends.
     * Null when the owner has no address to put as organiser; the email then goes without a calendar part. */
    private string? Calendar(Snapshot s, string method, IReadOnlyList<Booking> bookings, IReadOnlyList<BookingAttendee>? only, bool withLink)
    {
        var events = new List<CalendarEvent>();
        foreach (var b in bookings)
        {
            var owner = s.Users.GetValueOrDefault(b.OwnerUserId);
            if (owner == null || string.IsNullOrWhiteSpace(owner.Email)) return null;
            var line = s.Lines[b.Id];
            var attendees = (only ?? s.Attendees).Where(a => a.BookingId == b.Id).Select(a =>
                {
                    var user = a.UserId.HasValue ? s.Users.GetValueOrDefault(a.UserId.Value) : null;
                    var email = a.UserId.HasValue ? user?.Email : a.Email;
                    return string.IsNullOrWhiteSpace(email) ? null : new CalendarAttendee(user?.GetDisplayName(), email, a.Response);
                })
                .OfType<CalendarAttendee>().ToList();
            events.Add(new CalendarEvent(b.Id, b.Version, b.StartUtc, b.EndUtc, line.SpaceName,
                line.Place.Length > 0 ? $"{line.SpaceName} · {line.Place}" : line.SpaceName,
                withLink ? BookingLink(s, b.Id) : null, new CalendarPerson(owner.GetDisplayName(), owner.Email), attendees));
        }
        return BookingCalendar.Build(method, events, _clock.Now);
    }

    /* Everything the emails about these bookings need, loaded once: where and when each one is, the people invited,
     * and the accounts of the owners, the people invited and anyone else named (extraUserIds). Null (logged) when it
     * can't be loaded. */
    private async Task<Snapshot?> LoadAsync(IReadOnlyCollection<Booking> bookings, IEnumerable<Guid>? extraUserIds = null)
    {
        try
        {
            var ids = bookings.Select(b => b.Id).ToList();
            var attendees = await _attendees.GetListAsync(a => ids.Contains(a.BookingId));
            var userIds = bookings.Select(b => b.OwnerUserId)
                .Concat(attendees.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value))
                .Concat(extraUserIds ?? [])
                .Distinct().ToList();
            var users = (await _users.GetListByIdsAsync(userIds)).ToDictionary(u => u.Id);
            return new Snapshot(await LinesAsync(bookings), attendees, users, await BookingsUrlAsync());
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load what the emails for {Count} booking(s) need.", bookings.Count);
            return null;
        }
    }

    /* The reader's own time zone, or null (the building's is used). Looked up once per person per change. */
    private async Task<string?> ZoneOfAsync(Snapshot s, Guid? userId)
    {
        if (!userId.HasValue) return null;
        if (s.Zones.TryGetValue(userId.Value, out var known)) return known;
        string? zone = null;
        try
        {
            var value = await _settings.GetOrNullForUserAsync(PortalSettings.TimeZone, userId.Value, fallback: false);
            zone = !string.IsNullOrWhiteSpace(value) && BuildingCalendar.IsKnownZone(value.Trim()) ? value.Trim() : null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not read the time zone of user {UserId}.", userId);
        }
        s.Zones[userId.Value] = zone;
        return zone;
    }

    private static BookingEmailLine InZone(BookingEmailLine line, string? zone) => zone == null ? line : line with { TimeZoneId = zone };

    private async Task<Dictionary<Guid, BookingEmailLine>> LinesAsync(IEnumerable<Booking> bookings)
    {
        var contexts = new Dictionary<Guid, SpaceContext?>();
        var lines = new Dictionary<Guid, BookingEmailLine>();
        foreach (var b in bookings)
        {
            if (!contexts.TryGetValue(b.SpaceId, out var ctx))
            {
                ctx = await FindSpaceContextAsync(b.SpaceId);
                contexts[b.SpaceId] = ctx;
            }
            lines[b.Id] = ctx == null
                ? new BookingEmailLine("A space", "", b.StartUtc, b.EndUtc, "UTC")
                : new BookingEmailLine(ctx.Space.GetName("en"), Place(ctx), b.StartUtc, b.EndUtc, ctx.Building.TimeZone);
        }
        return lines;
    }

    private async Task<SpaceContext?> FindSpaceContextAsync(Guid spaceId)
    {
        try { return await _bookingManager.GetSpaceContextAsync(spaceId); }
        catch (Volo.Abp.UserFriendlyException) { return null; }
    }

    /* "HQ North · Floor 3", or just the building for a space without a floor. */
    private static string Place(SpaceContext ctx)
        => ctx.Floor == null ? ctx.Building.GetName("en") : $"{ctx.Building.GetName("en")} · Floor {ctx.Floor.GetName("en")}";

    private static string FirstName(IdentityUser user)
        => !string.IsNullOrWhiteSpace(user.Name) ? user.Name.Trim() : user.GetDisplayName();

    private async Task<string?> BookingsUrlAsync()
    {
        var root = await _urls.GetUrlOrNullAsync(PortalAppUrls.Spa);
        return string.IsNullOrWhiteSpace(root) ? null : $"{root.TrimEnd('/')}/{PortalAppUrls.BookingsPage}";
    }

    /* The bookings page with this booking open. Signing in first keeps the link. */
    private static string? BookingLink(Snapshot s, Guid bookingId) => s.BookingsUrl == null ? null : $"{s.BookingsUrl}?booking={bookingId}";

    private sealed record Snapshot(Dictionary<Guid, BookingEmailLine> Lines, List<BookingAttendee> Attendees,
        Dictionary<Guid, IdentityUser> Users, string? BookingsUrl)
    {
        public Dictionary<Guid, string?> Zones { get; } = new();
    }
}
