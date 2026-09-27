using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Dixels.Portal.Bookings.Queries;

public record GetBookingListQuery(BookingListFilterDto Filter) : IQuery<ListResultDto<BookingDto>>;

public class GetBookingListQueryHandler : IQueryHandler<GetBookingListQuery, ListResultDto<BookingDto>>
{
    private readonly IRepository<Booking, Guid> _bookings;
    private readonly BookingDtoMapper _mapper;
    private readonly IAsyncQueryableExecuter _async;

    public GetBookingListQueryHandler(IRepository<Booking, Guid> bookings, BookingDtoMapper mapper, IAsyncQueryableExecuter async)
    {
        _bookings = bookings;
        _mapper = mapper;
        _async = async;
    }

    public async Task<ListResultDto<BookingDto>> HandleAsync(GetBookingListQuery query)
    {
        var f = query.Filter;
        var q = await _bookings.GetQueryableAsync();
        if (!f.IncludeCancelled) q = q.Where(b => b.Status == BookingStatus.Confirmed);
        if (f.SpaceId.HasValue) q = q.Where(b => b.SpaceId == f.SpaceId.Value);
        if (f.OwnerUserId.HasValue) q = q.Where(b => b.OwnerUserId == f.OwnerUserId.Value);
        if (f.FromUtc.HasValue) q = q.Where(b => b.EndUtc > f.FromUtc.Value);
        if (f.ToUtc.HasValue) q = q.Where(b => b.StartUtc < f.ToUtc.Value);
        var list = await _async.ToListAsync(q.OrderBy(b => b.StartUtc));
        return new ListResultDto<BookingDto>(await _mapper.MapListAsync(list));
    }
}
