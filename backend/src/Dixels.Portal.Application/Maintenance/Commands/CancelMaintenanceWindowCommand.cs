using System;
using System.Threading.Tasks;
using Dixels.Portal.Cqrs;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.Maintenance.Commands;

/* Unblock a window. Cancelling twice is harmless. */
public record CancelMaintenanceWindowCommand(Guid Id) : ICommand<MaintenanceWindowDto>;

public class CancelMaintenanceWindowCommandHandler : ICommandHandler<CancelMaintenanceWindowCommand, MaintenanceWindowDto>
{
    private readonly IRepository<MaintenanceWindow, Guid> _maintenance;
    private readonly MaintenanceWindowDtoMapper _mapper;

    public CancelMaintenanceWindowCommandHandler(IRepository<MaintenanceWindow, Guid> maintenance, MaintenanceWindowDtoMapper mapper)
    {
        _maintenance = maintenance;
        _mapper = mapper;
    }

    public async Task<MaintenanceWindowDto> HandleAsync(CancelMaintenanceWindowCommand command)
    {
        var m = await _maintenance.FindAsync(command.Id)
            ?? throw new BusinessException(PortalDomainErrorCodes.MaintenanceNotFound, "No blocked time with that ID.");
        if (m.Status != MaintenanceStatus.Cancelled)
        {
            m.Status = MaintenanceStatus.Cancelled;
            await _maintenance.UpdateAsync(m, autoSave: true);
        }
        return await _mapper.MapAsync(m);
    }
}
