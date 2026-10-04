using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Languages;

/* The signed-in user's chosen language, kept on their account so it follows them to any device. */
public interface ILanguagePreferenceAppService : IApplicationService
{
    Task<LanguagePreferenceDto> GetAsync();
    Task SetAsync(LanguagePreferenceDto input);
}
