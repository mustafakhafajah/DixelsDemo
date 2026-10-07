using System;
using System.Threading.Tasks;
using Dixels.Portal.Bookings;
using Dixels.Portal.Estate;
using Dixels.Portal.Maintenance;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Portal.Controllers.Bookings;

/* Admin: the bookings in a building, floor or space that have not started yet, as a sub-collection of that
 * building, floor or space. GET .../count says how many; PATCH { lifecycle: "cancelled" } cancels them all, with an
 * optional message: { subject, message } for the cancellation emails. */
[RemoteService(Name = "Default")]
[Area("app")]
public class UpcomingBookingsController : PortalController
{
    private readonly IBookingAppService _bookings;

    public UpcomingBookingsController(IBookingAppService bookings)
    {
        _bookings = bookings;
    }

    [HttpGet("api/app/buildings/{id}/upcoming-bookings/count")]
    public Task<int> CountInBuildingAsync(Guid id) => CountAsync(MaintenanceScopeType.Building, id);

    [HttpGet("api/app/floors/{id}/upcoming-bookings/count")]
    public Task<int> CountOnFloorAsync(Guid id) => CountAsync(MaintenanceScopeType.Floor, id);

    [HttpGet("api/app/spaces/{id}/upcoming-bookings/count")]
    public Task<int> CountInSpaceAsync(Guid id) => CountAsync(MaintenanceScopeType.Space, id);

    [HttpPatch("api/app/buildings/{id}/upcoming-bookings")]
    public Task<CancelUpcomingResultDto> CancelInBuildingAsync(Guid id, [FromBody] CancellationDto input)
        => CancelAsync(MaintenanceScopeType.Building, id, input.Message);

    [HttpPatch("api/app/floors/{id}/upcoming-bookings")]
    public Task<CancelUpcomingResultDto> CancelOnFloorAsync(Guid id, [FromBody] CancellationDto input)
        => CancelAsync(MaintenanceScopeType.Floor, id, input.Message);

    [HttpPatch("api/app/spaces/{id}/upcoming-bookings")]
    public Task<CancelUpcomingResultDto> CancelInSpaceAsync(Guid id, [FromBody] CancellationDto input)
        => CancelAsync(MaintenanceScopeType.Space, id, input.Message);

    private Task<int> CountAsync(MaintenanceScopeType type, Guid id)
        => _bookings.GetUpcomingCountAsync(new EstateScopeDto { ScopeType = type, ScopeId = id });

    private Task<CancelUpcomingResultDto> CancelAsync(MaintenanceScopeType type, Guid id, CancellationMessageDto? message)
        => _bookings.CancelUpcomingAsync(new EstateScopeDto { ScopeType = type, ScopeId = id }, message);
}
