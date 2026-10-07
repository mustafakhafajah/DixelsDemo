using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Dixels.Portal.Bookings;

/* Outside guests' email addresses are only kept while the booking can still change. Once it has ended or been
 * cancelled (or deleted), each guest's address is wiped; the row stays, so the booking still says how many guests
 * it had. Run every hour by BookingGuestCleanupWorker. */
public class BookingGuestCleaner : ITransientDependency
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly IRepository<BookingAttendee, Guid> _attendees;
    private readonly IAsyncQueryableExecuter _executer;
    private readonly IDataFilter _dataFilter;
    private readonly IClock _clock;

    public BookingGuestCleaner(IRepository<Booking, Guid> bookings, IRepository<BookingAttendee, Guid> attendees,
        IAsyncQueryableExecuter executer, IDataFilter dataFilter, IClock clock)
    {
        _bookings = bookings;
        _attendees = attendees;
        _executer = executer;
        _dataFilter = dataFilter;
        _clock = clock;
    }

    /* How many addresses were wiped. */
    [UnitOfWork]
    public virtual async Task<int> ForgetFinishedAsync()
    {
        var now = _clock.Now;
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var finished = (await _bookings.GetQueryableAsync())
                .Where(b => b.Status == BookingStatus.Cancelled || b.EndUtc <= now || b.IsDeleted)
                .Select(b => b.Id);
            var due = await _executer.ToListAsync((await _attendees.GetQueryableAsync())
                .Where(a => a.UserId == null && a.Email != null && finished.Contains(a.BookingId)));
            if (due.Count == 0) return 0;
            foreach (var guest in due) guest.ForgetGuestEmail();
            await _attendees.UpdateManyAsync(due, autoSave: true);
            return due.Count;
        }
    }
}
