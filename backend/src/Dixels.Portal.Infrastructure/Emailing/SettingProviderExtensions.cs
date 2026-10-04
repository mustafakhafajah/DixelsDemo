using System.Threading.Tasks;
using Volo.Abp.Settings;

namespace Dixels.Portal.Emailing;

internal static class SettingProviderExtensions
{
    /* A setting the sender cannot work without; missing means a permanent failure with the setting's name in it. */
    public static async Task<string> GetRequiredAsync(this ISettingProvider settings, string name)
    {
        var value = await settings.GetOrNullAsync(name);
        return string.IsNullOrWhiteSpace(value) ? throw EmailDeliveryException.MissingSetting(name) : value.Trim();
    }
}
