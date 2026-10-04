using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dixels.Portal.Buildings;
using Dixels.Portal.Estate;
using Dixels.Portal.Floors;
using Dixels.Portal.Languages;
using Dixels.Portal.Localization;
using Dixels.Portal.Spaces;
using Dixels.Portal.SpaceTypes;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Localization;
using Volo.Abp.Validation;
using Xunit;

namespace Dixels.Portal.EntityFrameworkCore.Localization;

/* Every record has a required English name; other languages are optional extras. A reader sees their
 * language when the record has it, otherwise English, and never a third language. */
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
    public async Task Each_reader_gets_their_language_and_english_readers_only_ever_see_english()
    {
        var tag = Tag();
        var created = await _types.CreateAsync(Type($"Lounge {tag}", ("ar", $"استراحة {tag}")));

        using (CultureHelper.Use("en")) (await _types.GetAsync(created.Id)).Name.ShouldBe($"Lounge {tag}");
        using (CultureHelper.Use("ar")) (await _types.GetAsync(created.Id)).Name.ShouldBe($"استراحة {tag}");
        using (CultureHelper.Use("ar-SA")) (await _types.GetAsync(created.Id)).Name.ShouldBe($"استراحة {tag}");
        /* A language the portal doesn't speak reads as English. */
        using (CultureHelper.Use("fr")) (await _types.GetAsync(created.Id)).Name.ShouldBe($"Lounge {tag}");

        /* The edit form gets every language, English first. */
        created.Translations.Select(t => t.Language).ShouldBe(new[] { "en", "ar" });
    }

    [Fact]
    public async Task A_record_without_the_readers_language_shows_its_english_name()
    {
        var created = await _types.CreateAsync(Type($"Pod {Tag()}"));

        using (CultureHelper.Use("ar")) (await _types.GetAsync(created.Id)).Name.ShouldBe(created.Name);
    }

    [Fact]
    public async Task The_screen_language_does_not_decide_which_name_is_saved()
    {
        var tag = Tag();
        SpaceTypeDto created;
        using (CultureHelper.Use("ar")) created = await _types.CreateAsync(Type($"Booth {tag}", ("ar", $"كشك {tag}")));

        var rows = await TranslationsOfAsync(created.Id);
        rows["en"].ShouldBe($"Booth {tag}");
        rows["ar"].ShouldBe($"كشك {tag}");
    }

    [Fact]
    public async Task An_update_sends_the_full_set_so_a_language_left_out_is_removed()
    {
        var tag = Tag();
        var created = await _types.CreateAsync(Type($"Studio B {tag}", ("ar", $"استوديو ب {tag}")));

        await _types.UpdateAsync(created.Id, Type($"Studio B2 {tag}", ("ar", $"استوديو ب2 {tag}")));
        (await TranslationsOfAsync(created.Id))["ar"].ShouldBe($"استوديو ب2 {tag}");

        await _types.UpdateAsync(created.Id, Type($"Studio B2 {tag}"));
        (await TranslationsOfAsync(created.Id)).Keys.ShouldBe(new[] { "en" });
    }

    [Theory]
    [InlineData("en", "Extra", "validation.unsupported_language")]   // English has its own field
    [InlineData("xx", "Extra", "validation.unsupported_language")]   // not one of the portal's languages
    [InlineData("ar", "  ", "validation.missing_field")]             // added but left empty
    public async Task An_extra_language_must_be_a_supported_other_language_with_a_name(string language, string name, string code)
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _types.CreateAsync(Type($"Nook {Tag()}", (language, name))));
        ex.Code.ShouldBe(code);
    }

    /* Each box only takes its own language's letters; the error names the box it is about. */
    [Theory]
    [InlineData("غرفة", null, "name")]                    // Arabic letters in the English name
    [InlineData("Quiet room", "Quiet room", "translations.ar")] // Arabic name with only Latin letters
    public async Task Text_in_another_alphabet_is_rejected_on_its_own_field(string english, string? arabic, string field)
    {
        var extras = arabic == null ? [] : new[] { ("ar", $"{arabic} {Tag()}") };
        var ex = await Should.ThrowAsync<BusinessException>(() => _types.CreateAsync(Type($"{english} {Tag()}", extras)));
        ex.Code.ShouldBe(PortalDomainErrorCodes.WrongScript);
        ex.Data[ErrorFieldExtensions.FieldKey].ShouldBe(field);
    }

    [Fact]
    public async Task An_arabic_name_may_keep_a_latin_code()
    {
        var created = await _types.CreateAsync(Type($"VIP lounge {Tag()}", ("ar", $"صالة VIP {Tag()}")));
        created.Translations.Select(t => t.Language).ShouldBe(new[] { "en", "ar" });
    }

    [Fact]
    public async Task A_space_description_follows_the_same_letters_rule()
    {
        var (building, floor) = await CreateBuildingAsync(Tag());
        var ex = await Should.ThrowAsync<BusinessException>(() => _spaces.CreateAsync(new CreateUpdateSpaceDto
        {
            Name = $"Pod {Tag()}",
            Translations = [new() { Language = "ar", Name = $"كبسولة {Tag()}", Note = "Seats four" }],
            TypeId = DefaultSpaceTypes.MeetingRoom,
            BuildingId = building.Id,
            FloorId = floor.Id,
        }));
        ex.Code.ShouldBe(PortalDomainErrorCodes.WrongScript);
        ex.Data[ErrorFieldExtensions.FieldKey].ShouldBe("translations.ar.note");
    }

    [Fact]
    public async Task The_english_name_is_required()
    {
        /* Rejected by the request's own validation, before the rules run. */
        var ex = await Should.ThrowAsync<AbpValidationException>(() => _types.CreateAsync(Type(" ", ("ar", "ركن"))));
        ex.ValidationErrors.ShouldContain(e => e.MemberNames.Contains("Name"));
    }

    [Fact]
    public async Task Names_are_unique_within_a_language_and_the_error_points_at_that_language()
    {
        var tag = Tag();
        await _types.CreateAsync(Type($"Hall {tag}", ("ar", $"قاعة {tag}")));

        using (CultureHelper.Use("ar"))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() => _types.CreateAsync(Type($"Big hall {tag}", ("ar", $"قاعة {tag}"))));
            ex.Code.ShouldBe(PortalDomainErrorCodes.SpaceTypeDuplicate);
            ex.Data[ErrorFieldExtensions.FieldKey].ShouldBe("translations.ar");
            ex.Message.ShouldContain("يوجد بالفعل");
        }
    }

    [Fact]
    public async Task A_space_is_found_by_its_name_in_either_language_and_shown_in_the_readers()
    {
        var tag = Tag();
        var (building, floor) = await CreateBuildingAsync(tag);
        await _spaces.CreateAsync(new CreateUpdateSpaceDto
        {
            Name = $"Conference room 4 {tag}",
            Note = "Seats 10",
            Translations = [new() { Language = "ar", Name = $"غرفة الاجتماعات 4 {tag}", Note = "يتسع لعشرة" }],
            TypeId = DefaultSpaceTypes.MeetingRoom,
            BuildingId = building.Id,
            FloorId = floor.Id,
        });

        using (CultureHelper.Use("en"))
        {
            var found = (await _spaces.GetListAsync(new GetSpaceListInput { Bookable = true, Name = $"conference room 4 {tag}" })).Items.ShouldHaveSingleItem();
            found.Name.ShouldBe($"Conference room 4 {tag}");
            found.Note.ShouldBe("Seats 10");
        }
        using (CultureHelper.Use("ar"))
        {
            var found = (await _spaces.GetListAsync(new GetSpaceListInput { Name = $"غرفة الاجتماعات 4 {tag}", MaxResultCount = 10 })).Items.ShouldHaveSingleItem();
            found.Name.ShouldBe($"غرفة الاجتماعات 4 {tag}");
            found.Note.ShouldBe("يتسع لعشرة");
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

    private static CreateUpdateSpaceTypeDto Type(string english, params (string Language, string Name)[] extras) => new()
    {
        Name = english,
        Translations = extras.Select(e => new TranslationDto { Language = e.Language, Name = e.Name }).ToList(),
    };

    private Task<Dictionary<string, string>> TranslationsOfAsync(Guid typeId)
        => WithUnitOfWorkAsync(async () => (await _typeRepository.GetAsync(typeId)).Translations.ToDictionary(t => t.Language, t => t.Name));

    private Task<(Building, Floor)> CreateBuildingAsync(string tag)
        => WithUnitOfWorkAsync(async () =>
        {
            var building = await _buildings.InsertAsync(new Building(Guid.NewGuid(), $"Lingo {tag}"), autoSave: true);
            var floor = await _floors.InsertAsync(new Floor(Guid.NewGuid(), building.Id, "1"), autoSave: true);
            return (building, floor);
        });
}
