using System.Threading.Tasks;
using Dixels.Portal.TimeZones;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Portal.Controllers.TimeZones;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/time-zone-preference")]
public class TimeZonePreferenceController : PortalController, ITimeZonePreferenceAppService
{
    private readonly ITimeZonePreferenceAppService _preference;

    public TimeZonePreferenceController(ITimeZonePreferenceAppService preference)
    {
        _preference = preference;
    }

    /* { timeZone: "Asia/Amman" }, or { timeZone: null } when the user has never chosen one. */
    [HttpGet]
    public Task<TimeZonePreferenceDto> GetAsync() => _preference.GetAsync();

    /* { timeZone: "Asia/Amman" }; null or "" clears it. */
    [HttpPut]
    public Task SetAsync([FromBody] TimeZonePreferenceDto input) => _preference.SetAsync(input);
}
