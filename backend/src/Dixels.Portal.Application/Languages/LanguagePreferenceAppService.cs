using System.Threading.Tasks;
using Dixels.Portal.Localization;
using Dixels.Portal.Settings;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.SettingManagement;

namespace Dixels.Portal.Languages;

/* Stored as the user's own value of the Portal.Language setting (the AbpSettings table). It is deliberately not
 * ABP's default-language setting, so ABP's own pages, such as sign-in, are left as they are. */
[Authorize]
public class LanguagePreferenceAppService : PortalAppService, ILanguagePreferenceAppService
{
    private readonly ISettingManager _settings;

    public LanguagePreferenceAppService(ISettingManager settings)
    {
        _settings = settings;
    }

    /* Only the user's own choice, so the SPA can tell "never chosen". */
    public async Task<LanguagePreferenceDto> GetAsync()
    {
        var value = await _settings.GetOrNullForCurrentUserAsync(PortalSettings.Language, fallback: false);
        return new LanguagePreferenceDto { Language = PortalLanguages.IsSupported(value) ? PortalLanguages.Normalize(value) : null };
    }

    public async Task SetAsync(LanguagePreferenceDto input)
    {
        if (!PortalLanguages.IsSupported(input.Language))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UnsupportedLanguage, message: L["Error:UnsupportedLanguage"]).ForField("language");
        await _settings.SetForCurrentUserAsync(PortalSettings.Language, PortalLanguages.Normalize(input.Language));
    }
}
