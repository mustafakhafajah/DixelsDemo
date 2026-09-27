using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Bookings.Commands;

public record CreateBookingCommand(CreateBookingDto Input) : ICommand<BookingDto>;

public class CreateBookingCommandHandler : ICommandHandler<CreateBookingCommand, BookingDto>
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingOperations _operations;
    private readonly BookingDtoMapper _mapper;

    public CreateBookingCommandHandler(IRepository<Booking, Guid> bookings, BookingOperations operations, BookingDtoMapper mapper)
    {
        _bookings = bookings;
        _operations = operations;
        _mapper = mapper;
    }

    public async Task<BookingDto> HandleAsync(CreateBookingCommand command)
    {
        var input = command.Input;
        var key = string.IsNullOrWhiteSpace(input.IdempotencyKey) ? null : input.IdempotencyKey;

        /* A retried request (same key) gets the booking the first attempt made, not a second one. */
        if (key != null)
        {
            var existing = await _bookings.FirstOrDefaultAsync(b => b.IdempotencyKey == key);
            if (existing != null) return await _mapper.MapAsync(existing);
        }

        var booking = await _operations.CreateOneAsync(input.SpaceId, input.StartUtc, input.EndUtc, null, key);
        return await _mapper.MapAsync(booking);
    }
}
