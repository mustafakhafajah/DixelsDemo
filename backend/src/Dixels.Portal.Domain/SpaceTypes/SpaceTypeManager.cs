using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Localization;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes;

/* Space type rules: a name unique within each language, and a type in use can't be deleted. */
public class SpaceTypeManager : PortalDomainService
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Space, Guid> _spaces;

    public SpaceTypeManager(IRepository<SpaceType, Guid> types, IRepository<Space, Guid> spaces)
    {
        _types = types;
        _spaces = spaces;
    }

    /* The name is saved in the language the admin is using. */
    public async Task<SpaceType> CreateAsync(string name)
    {
        var language = PortalLanguages.Current;
        return new SpaceType(GuidGenerator.Create(), language, await CheckNameAsync(name, language, null));
    }

    /* Renames it in the admin's language only. Saving the fallback name unchanged adds no translation. */
    public async Task ChangeNameAsync(SpaceType type, string name)
    {
        var language = PortalLanguages.Current;
        var trimmed = name?.Trim() ?? "";
        if (type.FindTranslation(language) == null && trimmed == type.GetName(language)) return;
        type.SetName(language, await CheckNameAsync(trimmed, language, type.Id));
    }

    public async Task EnsureCanDeleteAsync(SpaceType type)
    {
        var used = await _spaces.CountAsync(s => s.TypeId == type.Id);
        if (used > 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceTypeInUse, message: used == 1
                ? L["Error:SpaceTypeInUse:One", type.GetName()]
                : L["Error:SpaceTypeInUse:Many", used, type.GetName()]);
    }

    /* Returns the trimmed name. excludeId is the type being renamed, so it doesn't clash with itself. */
    private async Task<string> CheckNameAsync(string? name, string language, Guid? excludeId)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:SpaceTypeNameMissing"]).ForField("name");
        var lower = trimmed.ToLower();
        if (await _types.AnyAsync(t => t.Id != excludeId && t.Translations.Any(x => x.Language == language && x.Name.ToLower() == lower)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceTypeDuplicate, message: L["Error:SpaceTypeDuplicate", trimmed]).ForField("name");
        return trimmed;
    }
}
