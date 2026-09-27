using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Dixels.Portal.Bookings.Commands;

/* Cancels this booking and every later one in its series; ended occurrences are left alone. */
public record CancelBookingSeriesFromCommand(Guid Id) : ICommand<CancelSeriesResultDto>;

public class CancelBookingSeriesFromCommandHandler : ICommandHandler<CancelBookingSeriesFromCommand, CancelSeriesResultDto>
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingOperations _operations;
    private readonly IClock _clock;

    public CancelBookingSeriesFromCommandHandler(IRepository<Booking, Guid> bookings, BookingOperations operations, IClock clock)
    {
        _bookings = bookings;
        _operations = operations;
        _clock = clock;
    }

    public async Task<CancelSeriesResultDto> HandleAsync(CancelBookingSeriesFromCommand command)
    {
        var anchor = await _operations.GetAsync(command.Id);
        await _operations.EnsureCanActAsync(anchor, "You can only cancel your own bookings.");
        if (!anchor.SeriesId.HasValue)
        {
            await _operations.CancelOneAsync(anchor);
            return new CancelSeriesResultDto { CancelledCount = 1 };
        }

        var seriesId = anchor.SeriesId.Value;
        var from = anchor.StartUtc;
        var targets = await _bookings.GetListAsync(b =>
            b.SeriesId == seriesId && b.Status == BookingStatus.Confirmed && b.StartUtc >= from);
        var count = 0;
        foreach (var b in targets.Where(b => b.GetLifecycle(_clock.Now) != TimeWindowState.Ended))
        {
            await _operations.CancelOneAsync(b);
            count++;
        }
        return new CancelSeriesResultDto { SeriesId = seriesId, CancelledCount = count };
    }
}
