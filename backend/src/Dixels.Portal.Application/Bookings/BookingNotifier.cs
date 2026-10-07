using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Spaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;
using Volo.Abp.Identity;
using Volo.Abp.Timing;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.Users;

namespace Dixels.Portal.Bookings;

/* Emails the person who booked when their booking is made, changed or cancelled, and schedules the reminder
 * 10 minutes before it starts. Called by the booking and blocked-time services right after they save.
 *
 * Emails are queued (IEmailSender.QueueAsync), so they go out in the background and only if the booking change is
 * saved. A problem building an email is logged and never stops the booking itself. Several bookings for one person
 * (a repeating series, or a bulk cancel) become one email listing them all. */
public class BookingNotifier : ITransientDependency
{
    public static readonly TimeSpan ReminderLead = TimeSpan.FromMinutes(10);

    private readonly IEmailSender _emailSender;
    private readonly IIdentityUserRepository _users;
    private readonly BookingManager _bookingManager;
    private readonly IBackgroundJobManager _jobs;
    private readonly IAppUrlProvider _urls;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ILogger<BookingNotifier> Logger { get; set; } = NullLogger<BookingNotifier>.Instance;

    public BookingNotifier(IEmailSender emailSender, IIdentityUserRepository users, BookingManager bookingManager,
        IBackgroundJobManager jobs, IAppUrlProvider urls, ICurrentUser currentUser, IClock clock)
    {
        _emailSender = emailSender;
        _users = users;
        _bookingManager = bookingManager;
        _jobs = jobs;
        _urls = urls;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task BookingsCreatedAsync(IReadOnlyCollection<Booking> bookings)
    {
        await PerOwnerAsync(bookings, (owner, lines, url) => BookingEmails.Confirmed(FirstName(owner), lines, url));
        foreach (var booking in bookings) await ScheduleReminderAsync(booking);
    }

    public async Task BookingRescheduledAsync(Booking booking, DateTime oldStartUtc, DateTime oldEndUtc)
    {
        await PerOwnerAsync([booking], (owner, lines, url) =>
            BookingEmails.Rescheduled(FirstName(owner), lines[0] with { StartUtc = oldStartUtc, EndUtc = oldEndUtc }, lines[0], url));
        /* The reminder already queued for the old time no longer matches the booking's version and does nothing. */
        await ScheduleReminderAsync(booking);
    }

    /* reason: shown in the email, e.g. "the space is blocked for maintenance". Someone cancelling another person's
     * booking (an admin) is mentioned as such; cancelling your own is not. */
    public Task BookingsCancelledAsync(IReadOnlyCollection<Booking> bookings, string? reason = null)
        => PerOwnerAsync(bookings, (owner, lines, url) =>
            BookingEmails.Cancelled(FirstName(owner), lines, cancelledByOther: _currentUser.Id != owner.Id, reason, url));

    public Task SendReminderAsync(Booking booking)
        => PerOwnerAsync([booking], (owner, lines, url) => BookingEmails.Reminder(FirstName(owner), lines[0], url));

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

    private async Task PerOwnerAsync(IReadOnlyCollection<Booking> bookings,
        Func<IdentityUser, IReadOnlyList<BookingEmailLine>, string?, EmailContent> compose)
    {
        if (bookings.Count == 0) return;
        try
        {
            var bookingsUrl = await BookingsUrlAsync();
            var lines = await LinesAsync(bookings);
            var owners = (await _users.GetListByIdsAsync(bookings.Select(b => b.OwnerUserId).Distinct()))
                .ToDictionary(u => u.Id);
            foreach (var group in bookings.GroupBy(b => b.OwnerUserId))
            {
                if (!owners.TryGetValue(group.Key, out var owner) || string.IsNullOrWhiteSpace(owner.Email)) continue;
                var email = compose(owner, group.OrderBy(b => b.StartUtc).Select(b => lines[b.Id]).ToList(), bookingsUrl);
                await _emailSender.QueueAsync(owner.Email, email.Subject, email.Html);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not queue the booking email for {Count} booking(s).", bookings.Count);
        }
    }

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
}
