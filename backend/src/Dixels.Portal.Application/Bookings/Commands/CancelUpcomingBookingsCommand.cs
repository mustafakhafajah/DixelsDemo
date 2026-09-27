using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Dixels.Portal.Bookings.Commands;

/* Admin: cancel every upcoming booking in a scope, e.g. after making it not bookable. */
public record CancelUpcomingBookingsCommand(EstateScopeDto Scope) : ICommand<CancelUpcomingResultDto>;

public class CancelUpcomingBookingsCommandHandler : ICommandHandler<CancelUpcomingBookingsCommand, CancelUpcomingResultDto>
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingOperations _operations;
    private readonly IAsyncQueryableExecuter _async;

    public CancelUpcomingBookingsCommandHandler(IRepository<Booking, Guid> bookings, BookingOperations operations,
        IAsyncQueryableExecuter async)
    {
        _bookings = bookings;
        _operations = operations;
        _async = async;
    }

    public async Task<CancelUpcomingResultDto> HandleAsync(CancelUpcomingBookingsCommand command)
    {
        var upcoming = await _async.ToListAsync(await _operations.UpcomingInScopeAsync(command.Scope));
        foreach (var b in upcoming) b.Cancel();
        await _bookings.UpdateManyAsync(upcoming, autoSave: true);
        return new CancelUpcomingResultDto { CancelledCount = upcoming.Count };
    }
}
