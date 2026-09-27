using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Dixels.Portal.Maintenance.Queries;

public record GetMaintenanceWindowListQuery(MaintenanceListFilterDto Filter) : IQuery<ListResultDto<MaintenanceWindowDto>>;

public class GetMaintenanceWindowListQueryHandler : IQueryHandler<GetMaintenanceWindowListQuery, ListResultDto<MaintenanceWindowDto>>
{
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly MaintenanceWindowDtoMapper _mapper;
    private readonly IAsyncQueryableExecuter _async;

    public GetMaintenanceWindowListQueryHandler(IRepository<MaintenanceWindow, Guid> maintenance,
        MaintenanceWindowDtoMapper mapper, IAsyncQueryableExecuter async)
    {
        _maintenance = maintenance;
        _mapper = mapper;
        _async = async;
    }

    public async Task<ListResultDto<MaintenanceWindowDto>> HandleAsync(GetMaintenanceWindowListQuery query)
    {
        var f = query.Filter;
        var q = await _maintenance.GetQueryableAsync();
        if (!f.IncludeCancelled) q = q.Where(m => m.Status == MaintenanceStatus.Active);
        if (f.SpaceId.HasValue) q = q.Where(m => m.SpaceId == f.SpaceId.Value);
        if (f.FromUtc.HasValue) q = q.Where(m => m.EndUtc > f.FromUtc.Value);
        if (f.ToUtc.HasValue) q = q.Where(m => m.StartUtc < f.ToUtc.Value);
        var list = await _async.ToListAsync(q.OrderBy(m => m.StartUtc));
        return new ListResultDto<MaintenanceWindowDto>(await _mapper.MapListAsync(list));
    }
}
