using System.Threading.Tasks;
using Dixels.Portal.TimeZones;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Profiles;

/* The signed-in user's time zone for booking emails, stored as their own Portal.TimeZone setting. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class TimeZonePreferenceTests : PortalEntityFrameworkCoreTestBase
{
    private readonly ITimeZonePreferenceAppService _preference;

    public TimeZonePreferenceTests()
    {
        _preference = GetRequiredService<ITimeZonePreferenceAppService>();
    }

    [Fact]
    public async Task A_chosen_zone_is_kept_and_can_be_cleared()
    {
        await _preference.SetAsync(new TimeZonePreferenceDto { TimeZone = " Asia/Amman " });
        (await _preference.GetAsync()).TimeZone.ShouldBe("Asia/Amman");

        await _preference.SetAsync(new TimeZonePreferenceDto { TimeZone = null });
        (await _preference.GetAsync()).TimeZone.ShouldBeNull();
    }

    [Fact]
    public async Task An_unknown_zone_is_refused()
    {
        foreach (var bad in new[] { "Mars/Olympus", "Arab Standard Time" })
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(() => _preference.SetAsync(new TimeZonePreferenceDto { TimeZone = bad }));
            ex.Code.ShouldBe(PortalDomainErrorCodes.UnknownTimeZone);
        }
    }
}
