using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp;
using Volo.Abp.Guids;

namespace Dixels.Portal.Bookings.Commands;

/* The client expands the recurrence rule; each surviving occurrence is created under one series. */
public record CreateBookingSeriesCommand(CreateBookingSeriesDto Input) : ICommand<CreateBookingSeriesResultDto>;

public class CreateBookingSeriesCommandHandler : ICommandHandler<CreateBookingSeriesCommand, CreateBookingSeriesResultDto>
{
    private readonly BookingOperations _operations;
    private readonly BookingDtoMapper _mapper;
    private readonly IGuidGenerator _guids;

    public CreateBookingSeriesCommandHandler(BookingOperations operations, BookingDtoMapper mapper, IGuidGenerator guids)
    {
        _operations = operations;
        _mapper = mapper;
        _guids = guids;
    }

    public async Task<CreateBookingSeriesResultDto> HandleAsync(CreateBookingSeriesCommand command)
    {
        var input = command.Input;
        var seriesId = input.Occurrences.Count > 1 ? _guids.Create() : (System.Guid?)null;
        var created = new List<Booking>();
        var skipped = new List<BookingWindowFailureDto>();

        /* An occurrence that breaks a rule is skipped and reported; the rest are still booked. */
        foreach (var o in input.Occurrences.OrderBy(o => o.StartUtc))
        {
            try
            {
                created.Add(await _operations.CreateOneAsync(input.SpaceId, o.StartUtc, o.EndUtc, input.Parking, seriesId, null));
            }
            catch (BusinessException ex)
            {
                skipped.Add(new BookingWindowFailureDto
                {
                    StartUtc = o.StartUtc, EndUtc = o.EndUtc,
                    ErrorCode = ex.Code ?? "unknown", ErrorMessage = ex.Message,
                });
            }
        }

        return new CreateBookingSeriesResultDto
        {
            SeriesId = created.Count > 0 ? seriesId : null,
            Created = await _mapper.MapListAsync(created),
            Skipped = skipped,
        };
    }
}
