using System.Threading.Tasks;
using Dixels.Portal.Estate;
using Dixels.Portal.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.SettingManagement;

namespace Dixels.Portal.TimeZones;

/* Stored as the user's own value of the Portal.TimeZone setting (the AbpSettings table), like their language.
 * Booking emails to them use it (BookingNotifier); without one they use each building's own zone. */
[Authorize]
public class TimeZonePreferenceAppService : PortalAppService, ITimeZonePreferenceAppService
{
    private readonly ISettingManager _settings;

    public TimeZonePreferenceAppService(ISettingManager settings)
    {
        _settings = settings;
    }

    /* Only the user's own choice, so the SPA can tell "never chosen". */
    public async Task<TimeZonePreferenceDto> GetAsync()
    {
        var value = await _settings.GetOrNullForCurrentUserAsync(PortalSettings.TimeZone, fallback: false);
        return new TimeZonePreferenceDto { TimeZone = !string.IsNullOrWhiteSpace(value) && BuildingCalendar.IsKnownZone(value) ? value : null };
    }

    /* Only IANA names (and "UTC"), as for buildings: the browser knows no others. An empty value clears the choice. */
    public async Task SetAsync(TimeZonePreferenceDto input)
    {
        var zone = input.TimeZone?.Trim();
        if (string.IsNullOrEmpty(zone))
        {
            await _settings.SetForCurrentUserAsync(PortalSettings.TimeZone, null);
            return;
        }
        if (!BuildingCalendar.IsKnownZone(zone))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UnknownTimeZone, message: L["Error:UnknownTimeZone", zone]).ForField("timeZone");
        await _settings.SetForCurrentUserAsync(PortalSettings.TimeZone, zone);
    }
}
