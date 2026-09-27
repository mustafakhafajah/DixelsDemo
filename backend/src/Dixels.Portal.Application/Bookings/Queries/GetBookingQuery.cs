using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;

namespace Dixels.Portal.Bookings.Queries;

public record GetBookingQuery(Guid Id) : IQuery<BookingDto>;

public class GetBookingQueryHandler : IQueryHandler<GetBookingQuery, BookingDto>
{
    private readonly BookingOperations _operations;
    private readonly BookingDtoMapper _mapper;

    public GetBookingQueryHandler(BookingOperations operations, BookingDtoMapper mapper)
    {
        _operations = operations;
        _mapper = mapper;
    }

    public async Task<BookingDto> HandleAsync(GetBookingQuery query)
        => await _mapper.MapAsync(await _operations.GetAsync(query.Id));
}
