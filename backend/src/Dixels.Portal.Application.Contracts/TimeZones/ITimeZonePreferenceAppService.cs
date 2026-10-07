using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.TimeZones;

/* The signed-in user's time zone for booking emails, kept on their account like their language. */
public interface ITimeZonePreferenceAppService : IApplicationService
{
    Task<TimeZonePreferenceDto> GetAsync();
    Task SetAsync(TimeZonePreferenceDto input);
}
