using Shouldly;
using Xunit;

namespace Dixels.Portal.Localization;

/* ScriptRules is pure, so these need no database or ABP base class. */
public class ScriptRulesTests
{
    [Theory]
    [InlineData("Conference Room 4")]
    [InlineData("Café Zoë")]            // accented Latin letters
    [InlineData("AV-Cart #01 (2nd)")]   // digits and punctuation
    [InlineData("2")]                   // no letters at all
    public void English_text_in_latin_letters_fits(string text) => ScriptRules.Fits(LanguageScript.Latin, text).ShouldBeTrue();

    [Theory]
    [InlineData("غرفة الاجتماعات 4")]
    [InlineData("Room غرفة")]
    public void English_text_with_arabic_letters_does_not_fit(string text) => ScriptRules.Fits(LanguageScript.Latin, text).ShouldBeFalse();

    [Theory]
    [InlineData("غرفة الاجتماعات ٤")]   // Arabic-Indic digits
    [InlineData("غرفة VIP")]            // a Latin code next to Arabic letters
    [InlineData("قاعةٌ")]               // diacritics are not letters
    [InlineData("2")]
    public void Arabic_text_with_arabic_letters_fits(string text) => ScriptRules.Fits(LanguageScript.Arabic, text).ShouldBeTrue();

    [Theory]
    [InlineData("Conference Room 4")]   // only Latin letters
    [InlineData("Комната")]             // another alphabet
    [InlineData("غرفة Комната")]
    public void Arabic_text_without_arabic_letters_or_with_another_alphabet_does_not_fit(string text)
        => ScriptRules.Fits(LanguageScript.Arabic, text).ShouldBeFalse();

    [Fact]
    public void English_text_in_another_alphabet_does_not_fit() => ScriptRules.Fits(LanguageScript.Latin, "Комната").ShouldBeFalse();
}
