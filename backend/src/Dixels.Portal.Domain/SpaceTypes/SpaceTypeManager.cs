using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Localization;
using Dixels.Portal.Spaces;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Dixels.Portal.SpaceTypes;

/* Space type rules: an English name, optional names in other languages, each unique within its language;
 * and a type in use can't be deleted. */
public class SpaceTypeManager : PortalDomainService
{
    private readonly IRepository<SpaceType, Guid> _types;
    private readonly IRepository<Space, Guid> _spaces;

    public SpaceTypeManager(IRepository<SpaceType, Guid> types, IRepository<Space, Guid> spaces)
    {
        _types = types;
        _spaces = spaces;
    }

    public async Task<SpaceType> CreateAsync(string name, IEnumerable<NameTranslation>? translations)
    {
        var english = await CheckNameAsync(PortalLanguages.Default, name, null, "name");
        var extras = await CheckTranslationsAsync(translations, null);
        var type = new SpaceType(GuidGenerator.Create(), english);
        foreach (var t in extras) type.SetName(t.Language, t.Name);
        return type;
    }

    /* translations is the full set of extra languages: one left out is removed. */
    public async Task ChangeNamesAsync(SpaceType type, string name, IEnumerable<NameTranslation>? translations)
    {
        var english = await CheckNameAsync(PortalLanguages.Default, name, type.Id, "name");
        var extras = await CheckTranslationsAsync(translations, type.Id);
        type.SetName(PortalLanguages.Default, english);
        foreach (var t in extras) type.SetName(t.Language, t.Name);
        type.RemoveTranslationsExcept(extras.Select(t => t.Language));
    }

    public async Task EnsureCanDeleteAsync(SpaceType type)
    {
        var used = await _spaces.CountAsync(s => s.TypeId == type.Id);
        if (used > 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceTypeInUse, message: used == 1
                ? L["Error:SpaceTypeInUse:One", type.GetName()]
                : L["Error:SpaceTypeInUse:Many", used, type.GetName()]);
    }

    private async Task<List<NameTranslation>> CheckTranslationsAsync(IEnumerable<NameTranslation>? input, Guid? excludeId)
    {
        var extras = TranslationRules.Clean(L, input, SpaceTypeConsts.MaxNameLength);
        foreach (var t in extras) await CheckNameAsync(t.Language, t.Name, excludeId, $"translations.{t.Language}");
        return extras;
    }

    /* Returns the trimmed name. excludeId is the type being renamed, so it doesn't clash with itself. */
    private async Task<string> CheckNameAsync(string language, string? name, Guid? excludeId, string field)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
            throw new UserFriendlyException(code: PortalDomainErrorCodes.MissingField, message: L["Error:SpaceTypeNameMissing"]).ForField(field);
        ScriptRules.EnsureFits(L, language, trimmed, field);
        var lower = trimmed.ToLower();
        if (await _types.AnyAsync(t => t.Id != excludeId && t.Translations.Any(x => x.Language == language && x.Name.ToLower() == lower)))
            throw new UserFriendlyException(code: PortalDomainErrorCodes.SpaceTypeDuplicate, message: L["Error:SpaceTypeDuplicate", trimmed]).ForField(field);
        return trimmed;
    }
}
