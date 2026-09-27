using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Maintenance.Queries;

public record GetMaintenanceWindowQuery(Guid Id) : IQuery<MaintenanceWindowDto>;

public class GetMaintenanceWindowQueryHandler : IQueryHandler<GetMaintenanceWindowQuery, MaintenanceWindowDto>
{
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly MaintenanceWindowDtoMapper _mapper;

    public GetMaintenanceWindowQueryHandler(IRepository<MaintenanceWindow, Guid> maintenance, MaintenanceWindowDtoMapper mapper)
    {
        _maintenance = maintenance;
        _mapper = mapper;
    }

    public async Task<MaintenanceWindowDto> HandleAsync(GetMaintenanceWindowQuery query)
    {
        var window = await _maintenance.FindAsync(query.Id)
            ?? throw new BusinessException(PortalDomainErrorCodes.MaintenanceNotFound, "No blocked time with that ID.");
        return await _mapper.MapAsync(window);
    }
}
