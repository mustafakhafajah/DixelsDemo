using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings.Commands;
using Dixels.Portal.Bookings.Queries;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Bookings;

/* Any signed-in user may book. "Only your own booking unless you're an admin" depends on the booking,
 * so that check lives in BookingOperations rather than in an [Authorize] attribute. */
[Authorize]
public class BookingAppService : PortalAppService, IBookingAppService
{
    private readonly ICommandDispatcher _commands;
    private readonly IQueryDispatcher _queries;

    public BookingAppService(ICommandDispatcher commands, IQueryDispatcher queries)
    {
        _commands = commands;
        _queries = queries;
    }

    public Task<ListResultDto<BookingDto>> GetListAsync(BookingListFilterDto input)
        => _queries.QueryAsync(new GetBookingListQuery(input));

    public Task<BookingDto> GetAsync(Guid id)
        => _queries.QueryAsync(new GetBookingQuery(id));

    public Task<BookingDto> CreateAsync(CreateBookingDto input)
        => _commands.SendAsync(new CreateBookingCommand(input));

    public Task<CreateBookingSeriesResultDto> CreateSeriesAsync(CreateBookingSeriesDto input)
        => _commands.SendAsync(new CreateBookingSeriesCommand(input));

    public Task<BookingDto> RescheduleAsync(Guid id, RescheduleBookingDto input)
        => _commands.SendAsync(new RescheduleBookingCommand(id, input));

    public Task<BookingDto> CancelAsync(Guid id)
        => _commands.SendAsync(new CancelBookingCommand(id));

    public Task<CancelSeriesResultDto> CancelSeriesFromAsync(Guid id)
        => _commands.SendAsync(new CancelBookingSeriesFromCommand(id));

    public Task<BookingDto> EndEarlyAsync(Guid id)
        => _commands.SendAsync(new EndBookingEarlyCommand(id));

    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public Task<int> GetUpcomingCountAsync(EstateScopeDto input)
        => _queries.QueryAsync(new GetUpcomingBookingCountQuery(input));

    [Authorize(PortalPermissions.Bookings.ManageAll)]
    public Task<CancelUpcomingResultDto> CancelUpcomingAsync(EstateScopeDto input)
        => _commands.SendAsync(new CancelUpcomingBookingsCommand(input));
}
