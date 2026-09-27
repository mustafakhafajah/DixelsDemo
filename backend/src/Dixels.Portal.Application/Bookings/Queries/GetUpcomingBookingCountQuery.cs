using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Volo.Abp.Linq;

namespace Dixels.Portal.Bookings.Queries;

/* Admin: bookings in a space, floor or building that have not started yet (confirmed only). */
public record GetUpcomingBookingCountQuery(EstateScopeDto Scope) : IQuery<int>;

public class GetUpcomingBookingCountQueryHandler : IQueryHandler<GetUpcomingBookingCountQuery, int>
{
    private readonly BookingOperations _operations;
    private readonly IAsyncQueryableExecuter _async;

    public GetUpcomingBookingCountQueryHandler(BookingOperations operations, IAsyncQueryableExecuter async)
    {
        _operations = operations;
        _async = async;
    }

    public async Task<int> HandleAsync(GetUpcomingBookingCountQuery query)
        => await _async.CountAsync(await _operations.UpcomingInScopeAsync(query.Scope));
}
