using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace Dixels.Portal.Emailing;

/* Picks the sign-in strategy named by ExchangeOnline.AuthType, read on every send so a settings change needs no restart. */
public class SmtpAuthStrategyResolver : ITransientDependency
{
    private readonly IEnumerable<ISmtpAuthStrategy> _strategies;
    private readonly ISettingProvider _settings;

    public SmtpAuthStrategyResolver(IEnumerable<ISmtpAuthStrategy> strategies, ISettingProvider settings)
    {
        _strategies = strategies;
        _settings = settings;
    }

    public async Task<ISmtpAuthStrategy> ResolveAsync()
    {
        var authType = await GetAuthTypeAsync(_settings);
        return _strategies.FirstOrDefault(s => s.AuthType == authType)
            ?? throw new EmailDeliveryException($"No sign-in strategy is registered for {authType}.", isTransient: false);
    }

    /* "OAuth2" or "Basic", any letter case; anything else is a configuration mistake worth a clear message. */
    public static async Task<ExchangeAuthType> GetAuthTypeAsync(ISettingProvider settings)
    {
        var value = await settings.GetOrNullAsync(ExchangeOnlineSettingNames.AuthType);
        if (Enum.TryParse<ExchangeAuthType>(value, ignoreCase: true, out var authType) && Enum.IsDefined(authType) && !int.TryParse(value, out _))
        {
            return authType;
        }

        throw new EmailDeliveryException(
            $"The {ExchangeOnlineSettingNames.AuthType} setting is \"{value}\". Use \"{nameof(ExchangeAuthType.OAuth2)}\" or \"{nameof(ExchangeAuthType.Basic)}\".",
            isTransient: false,
            settingName: ExchangeOnlineSettingNames.AuthType);
    }
}
