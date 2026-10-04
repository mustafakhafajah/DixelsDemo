using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Dixels.Portal.Notifications;

/* Queued when a booking is made or moved, to run 10 minutes before it starts (ABP keeps it in the background-job
 * table until then). Version = the booking's version at that moment: a booking that was changed or cancelled since
 * has a higher version, so this reminder does nothing and the one queued with the change (if any) takes over. */
[BackgroundJobName("BookingReminderJob")]
public class BookingReminderJobArgs
{
    public Guid BookingId { get; set; }

    public int Version { get; set; }
}

public class BookingReminderJob : AsyncBackgroundJob<BookingReminderJobArgs>, ITransientDependency
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingNotifier _notifier;
    private readonly IUnitOfWorkManager _unitOfWork;
    private readonly IClock _clock;

    public BookingReminderJob(IRepository<Booking, Guid> bookings, BookingNotifier notifier, IUnitOfWorkManager unitOfWork, IClock clock)
    {
        _bookings = bookings;
        _notifier = notifier;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public override async Task ExecuteAsync(BookingReminderJobArgs args)
    {
        using var uow = _unitOfWork.Begin(requiresNew: true);
        var booking = await _bookings.FindAsync(args.BookingId);
        if (booking != null && IsStillDue(booking, args.Version, _clock.Now))
        {
            await _notifier.SendReminderAsync(booking);
        }
        else
        {
            Logger.LogDebug("Booking {BookingId} changed, was cancelled or has started; no reminder sent.", args.BookingId);
        }
        await uow.CompleteAsync();
    }

    /* Still confirmed, unchanged since the reminder was queued, and not started yet (a reminder that is late, e.g.
     * because the site was stopped, is dropped rather than sent after the booking began). */
    public static bool IsStillDue(Booking booking, int queuedVersion, DateTime nowUtc)
        => booking.Status == BookingStatus.Confirmed && booking.Version == queuedVersion && nowUtc < booking.StartUtc;
}
