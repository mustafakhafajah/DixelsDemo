using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;

namespace Dixels.Portal.Bookings.Commands;

public record CancelBookingCommand(Guid Id) : ICommand<BookingDto>;

public class CancelBookingCommandHandler : ICommandHandler<CancelBookingCommand, BookingDto>
{
    private readonly BookingOperations _operations;
    private readonly BookingDtoMapper _mapper;

    public CancelBookingCommandHandler(BookingOperations operations, BookingDtoMapper mapper)
    {
        _operations = operations;
        _mapper = mapper;
    }

    public async Task<BookingDto> HandleAsync(CancelBookingCommand command)
    {
        var b = await _operations.GetAsync(command.Id);
        await _operations.CancelOneAsync(b);
        return await _mapper.MapAsync(b);
    }
}
