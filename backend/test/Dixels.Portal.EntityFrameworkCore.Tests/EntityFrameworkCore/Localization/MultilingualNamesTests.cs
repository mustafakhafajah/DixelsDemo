using System;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Languages;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Localization;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Localization;

/* One Name field in the form, saved in the language the admin is using; readers get their language,
 * else English, else whichever language has a name. */
[Collection(PortalTestConsts.CollectionDefinitionName)]
public class MultilingualNamesTests : PortalEntityFrameworkCoreTestBase
{
    private readonly ISpaceTypeAppService _types;
    private readonly ISpaceAppService _spaces;
    private readonly ILanguagePreferenceAppService _preference;
    private readonly IRepository<SpaceType, Guid> _typeRepository;
    private readonly IRepository<Building, Guid> _buildings;
    private readonly IRepository<Floor, Guid> _floors;

    public MultilingualNamesTests()
    {
        _types = GetRequiredService<ISpaceTypeAppService>();
        _spaces = GetRequiredService<ISpaceAppService>();
        _preference = GetRequiredService<ILanguagePreferenceAppService>();
        _typeRepository = GetRequiredService<IRepository<SpaceType, Guid>>();
        _buildings = GetRequiredService<IRepository<Building, Guid>>();
        _floors = GetRequiredService<IRepository<Floor, Guid>>();
    }

    [Fact]
    public async Task A_name_saved_in_each_language_is_read_back_in_that_language()
    {
        var tag = Tag();
        SpaceTypeDto created;
        using (CultureHelper.Use("en")) created = await _types.CreateAsync(new() { Name = $"Lounge {tag}" });
        using (CultureHelper.Use("ar")) await _types.UpdateAsync(created.Id, new() { Name = $"استراحة {tag}" });

        using (CultureHelper.Use("en")) (await _types.GetAsync(created.Id)).Name.ShouldBe($"Lounge {tag}");
        using (CultureHelper.Use("ar"))
        {
            var ar = await _types.GetAsync(created.Id);
            ar.Name.ShouldBe($"استراحة {tag}");
            ar.IsTranslated.ShouldBeTrue();
        }
        /* A regional culture uses its language's name. */
        using (CultureHelper.Use("ar-SA")) (await _types.GetAsync(created.Id)).Name.ShouldBe($"استراحة {tag}");

        var rows = await WithUnitOfWorkAsync(async () => (await _typeRepository.GetAsync(created.Id)).Translations.Select(t => t.Language).OrderBy(l => l).ToList());
        rows.ShouldBe(new[] { "ar", "en" });
    }

    [Fact]
    public async Task A_name_missing_in_the_readers_language_falls_back_to_english_then_to_any()
    {
        var tag = Tag();
        SpaceTypeDto english, arabicOnly;
        using (CultureHelper.Use("en")) english = await _types.CreateAsync(new() { Name = $"Pod {tag}" });
        using (CultureHelper.Use("ar")) arabicOnly = await _types.CreateAsync(new() { Name = $"كبسولة {tag}" });

        using (CultureHelper.Use("ar"))
        {
            var fallback = await _types.GetAsync(english.Id);
            fallback.Name.ShouldBe($"Pod {tag}");
            fallback.IsTranslated.ShouldBeFalse();
        }
        using (CultureHelper.Use("en")) (await _types.GetAsync(arabicOnly.Id)).Name.ShouldBe($"كبسولة {tag}");
        /* A language the portal doesn't speak reads as the default language. */
        using (CultureHelper.Use("fr")) (await _types.GetAsync(english.Id)).Name.ShouldBe($"Pod {tag}");
    }

    [Fact]
    public async Task Saving_the_fallback_name_unchanged_adds_no_translation()
    {
        SpaceTypeDto created;
        using (CultureHelper.Use("en")) created = await _types.CreateAsync(new() { Name = $"Booth {Tag()}" });

        using (CultureHelper.Use("ar"))
        {
            var saved = await _types.UpdateAsync(created.Id, new() { Name = created.Name });
            saved.IsTranslated.ShouldBeFalse();
        }
        var languages = await WithUnitOfWorkAsync(async () => (await _typeRepository.GetAsync(created.Id)).Translations.Select(t => t.Language).ToList());
        languages.ShouldBe(new[] { "en" });
    }

    [Fact]
    public async Task Names_are_unique_within_a_language_only()
    {
        var name = $"Hall {Tag()}";
        using (CultureHelper.Use("en")) await _types.CreateAsync(new() { Name = name });

        /* The same text as another type's Arabic name is fine... */
        using (CultureHelper.Use("ar")) await _types.CreateAsync(new() { Name = name });
        /* ...but not twice in one language, and the message is in that language. */
        using (CultureHelper.Use("ar"))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() => _types.CreateAsync(new() { Name = name.ToUpperInvariant() }));
            ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceTypeDuplicate);
            ex.Message.ShouldContain("يوجد بالفعل");
        }
    }

    [Fact]
    public async Task A_space_is_found_by_its_name_in_any_language_and_keeps_its_note_per_language()
    {
        var tag = Tag();
        var (building, floor) = await CreateBuildingAsync(tag);
        SpaceDto space;
        using (CultureHelper.Use("en"))
            space = await _spaces.CreateAsync(NewSpace(building, floor, $"Board room {tag}", "Seats 10"));
        using (CultureHelper.Use("ar"))
            await _spaces.UpdateAsync(space.Id, NewSpace(building, floor, $"قاعة الاجتماعات {tag}", "يتسع لعشرة"));

        using (CultureHelper.Use("en"))
        {
            var found = await _spaces.GetBookableListAsync(new FindSpacesInput { Name = $"الاجتماعات {tag}" });
            found.Items.ShouldHaveSingleItem().Name.ShouldBe($"Board room {tag}");
            found.Items[0].Note.ShouldBe("Seats 10");
        }
        using (CultureHelper.Use("ar"))
        {
            var page = await _spaces.GetPagedListAsync(new GetSpacesInput { Name = $"board room {tag}", MaxResultCount = 10 });
            page.Items.ShouldHaveSingleItem().Name.ShouldBe($"قاعة الاجتماعات {tag}");
            page.Items[0].Note.ShouldBe("يتسع لعشرة");
        }
    }

    [Fact]
    public async Task The_users_language_is_saved_and_only_a_supported_one_is_accepted()
    {
        await _preference.SetAsync(new LanguagePreferenceDto { Language = "ar" });
        (await _preference.GetAsync()).Language.ShouldBe("ar");

        var ex = await Should.ThrowAsync<BusinessException>(() => _preference.SetAsync(new LanguagePreferenceDto { Language = "xx" }));
        ex.Code.ShouldBe(PortalDomainErrorCodes.UnsupportedLanguage);
        (await _preference.GetAsync()).Language.ShouldBe("ar");

        await _preference.SetAsync(new LanguagePreferenceDto { Language = "en" });
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static CreateUpdateSpaceDto NewSpace(Building building, Floor floor, string name, string note) => new()
    {
        Name = name,
        Note = note,
        TypeId = DefaultSpaceTypes.MeetingRoom,
        BuildingId = building.Id,
        FloorId = floor.Id,
    };

    private Task<(Building, Floor)> CreateBuildingAsync(string tag)
        => WithUnitOfWorkAsync(async () =>
        {
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), "en", $"Lingo {tag}"), autoSave: true);
            var floor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "en", "1"), autoSave: true);
            return (building, floor);
        });
}
