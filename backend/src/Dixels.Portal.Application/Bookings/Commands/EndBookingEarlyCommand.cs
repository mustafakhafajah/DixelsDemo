using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Dixels.Portal.Estate;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Dixels.Portal.Bookings.Commands;

/* Frees the space now: the booking's end moves to the current time. */
public record EndBookingEarlyCommand(Guid Id) : ICommand<BookingDto>;

public class EndBookingEarlyCommandHandler : ICommandHandler<EndBookingEarlyCommand, BookingDto>
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingOperations _operations;
    private readonly BookingDtoMapper _mapper;
    private readonly IClock _clock;

    public EndBookingEarlyCommandHandler(IRepository<Booking, Guid> bookings, BookingOperations operations,
        BookingDtoMapper mapper, IClock clock)
    {
        _bookings = bookings;
        _operations = operations;
        _mapper = mapper;
        _clock = clock;
    }

    public async Task<BookingDto> HandleAsync(EndBookingEarlyCommand command)
    {
        var b = await _operations.GetAsync(command.Id);
        await _operations.EnsureCanActAsync(b, "You can only end your own bookings.");
        if (b.GetLifecycle(_clock.Now) != TimeWindowState.InProgress)
            throw new BusinessException(PortalDomainErrorCodes.BookingNotInProgress,
                "Only a booking that has already started can be ended early.");
        b.EndUtc = _clock.Now;
        b.Version++;
        await _bookings.UpdateAsync(b, autoSave: true);
        return await _mapper.MapAsync(b);
    }
}
