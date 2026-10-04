using System.Threading.Tasks;
using Dixels.Portal.Languages;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Portal.Controllers.Languages;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/language-preference")]
public class LanguagePreferenceController : PortalController, ILanguagePreferenceAppService
{
    private readonly ILanguagePreferenceAppService _preference;

    public LanguagePreferenceController(ILanguagePreferenceAppService preference)
    {
        _preference = preference;
    }

    /* { language: "ar" }, or { language: null } when the user has never chosen one. */
    [HttpGet]
    public Task<LanguagePreferenceDto> GetAsync() => _preference.GetAsync();

    [HttpPut]
    public Task SetAsync([FromBody] LanguagePreferenceDto input) => _preference.SetAsync(input);
}
