using System.Threading.Tasks;
using Dixels.Portal.Localization;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Localization;
using Volo.Abp.SettingManagement;

namespace Dixels.Portal.Languages;

/* Stored as the user's own value of ABP's default-language setting (the AbpSettings table), so ABP's own
 * pages, such as sign-in, also open in it when the request names no language. */
[Authorize]
public class LanguagePreferenceAppService : PortalAppService, ILanguagePreferenceAppService
{
    private readonly ISettingManager _settings;

    public LanguagePreferenceAppService(ISettingManager settings)
    {
        _settings = settings;
    }

    /* Only the user's own choice: no fallback to the site default, so the SPA can tell "never chosen". */
    public async Task<LanguagePreferenceDto> GetAsync()
    {
        var value = await _settings.GetOrNullForCurrentUserAsync(LocalizationSettingNames.DefaultLanguage, fallback: false);
        return new LanguagePreferenceDto { Language = PortalLanguages.IsSupported(value) ? PortalLanguages.Normalize(value) : null };
    }

    public async Task SetAsync(LanguagePreferenceDto input)
    {
        if (!PortalLanguages.IsSupported(input.Language))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.UnsupportedLanguage, message: L["Error:UnsupportedLanguage"]).ForField("language");
        await _settings.SetForCurrentUserAsync(LocalizationSettingNames.DefaultLanguage, PortalLanguages.Normalize(input.Language));
    }
}
