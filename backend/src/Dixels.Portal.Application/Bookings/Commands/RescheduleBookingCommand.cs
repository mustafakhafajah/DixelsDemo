using System;
using System.Threading.Tasks;
using Dixels.Portal.Common;
using Dixels.Portal.Cqrs;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Dixels.Portal.Bookings.Commands;

public record RescheduleBookingCommand(Guid Id, RescheduleBookingDto Input) : ICommand<BookingDto>;

public class RescheduleBookingCommandHandler : ICommandHandler<RescheduleBookingCommand, BookingDto>
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingManager _manager;
    private readonly BookingOperations _operations;
    private readonly BookingDtoMapper _mapper;
    private readonly IClock _clock;

    public RescheduleBookingCommandHandler(IRepository<Booking, Guid> bookings, BookingManager manager,
        BookingOperations operations, BookingDtoMapper mapper, IClock clock)
    {
        _bookings = bookings;
        _manager = manager;
        _operations = operations;
        _mapper = mapper;
        _clock = clock;
    }

    public async Task<BookingDto> HandleAsync(RescheduleBookingCommand command)
    {
        var input = command.Input;
        var b = await _operations.GetAsync(command.Id);
        await _operations.EnsureCanActAsync(b, "You can only change your own bookings.");
        var state = b.GetLifecycle(_clock.Now);
        if (state is "ended" or "cancelled")
            throw new BusinessException(PortalDomainErrorCodes.BookingLocked, $"A {state} booking cannot be changed.");
        if (state == "in_progress")
            throw new BusinessException(PortalDomainErrorCodes.BookingInProgress,
                "A booking that has started can only be cancelled or ended early.");
        /* Optimistic concurrency: refuse if someone changed the booking after the client loaded it. */
        if (input.ExpectedVersion.HasValue && input.ExpectedVersion.Value != b.Version)
            throw new BusinessException(PortalDomainErrorCodes.VersionMismatch,
                    "This booking changed since you loaded it. Reload and try again.")
                .WithData("expected", input.ExpectedVersion.Value)
                .WithData("current", b.Version);

        var start = input.StartUtc.AsUtc();
        var end = input.EndUtc.AsUtc();
        var ctx = await _manager.GetSpaceContextAsync(b.SpaceId);
        _manager.ValidateWindow(ctx, start, end);
        await _manager.EnsureNoMaintenanceAsync(ctx, start, end);
        await _manager.EnsureNoConflictAsync(b.SpaceId, start, end, b.Id);
        await _manager.EnsureNoSelfOverlapAsync(b.OwnerUserId, b.SpaceId, start, end, b.Id);

        b.StartUtc = start;
        b.EndUtc = end;
        b.Version++;
        await _bookings.UpdateAsync(b, autoSave: true);
        return await _mapper.MapAsync(b);
    }
}
