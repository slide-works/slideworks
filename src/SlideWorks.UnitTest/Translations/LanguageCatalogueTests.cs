using SlideWorks.Core.Translations;

namespace SlideWorks.UnitTest.Translations;

// src/contracts/languages.json against K1 and D10.
[TestFixture]
public class LanguageCatalogueTests
{
    [Test]
    public void All_WhenRead_ShouldListFifteenCodes()
    {
        LanguageCatalogue.All.Select(language => language.Code)
            .ShouldBe(["en", "es", "fr", "de", "it", "pt", "nl", "id", "ms", "vi", "th", "ja", "ko", "zh-Hans", "zh-Hant"]);
    }

    [Test]
    public void IsRightToLeft_WhenArabic_ShouldBeTrue()
    {
        LanguageCatalogue.IsRightToLeft("ar").ShouldBeTrue();
        LanguageCatalogue.IsRightToLeft("th").ShouldBeFalse();
        LanguageCatalogue.Find("ar").ShouldBeNull();
    }

    [Test]
    public void Find_WhenCodeDiffersInCase_ShouldBeNull()
    {
        LanguageCatalogue.Find("zh-Hans")!.EnglishName.ShouldBe("Chinese (Simplified)");
        LanguageCatalogue.Find("zh-hans").ShouldBeNull();
        LanguageCatalogue.Find("TH").ShouldBeNull();
    }

    [Test]
    public void Short_WhenRead_ShouldBeCapitalCodeForLatinElseName()
    {
        foreach (var language in LanguageCatalogue.All)
        {
            var expected = language.Script == "latin" ? language.Code.ToUpperInvariant() : language.Name;
            language.Short.ShouldBe(expected, language.Code);
            LanguageCatalogue.ScriptOf(language.Code).ShouldNotBeNull(language.Code);
        }
    }

    [Test]
    public void ScriptOf_WhenThai_ShouldNameItsNotoFont()
    {
        LanguageCatalogue.ScriptOf("th")!.Font.ShouldBe("Noto Sans Thai");
        LanguageCatalogue.ScriptOf("en")!.ShouldBe(new LanguageScript(null, null));
        LanguageCatalogue.ScriptOf("xx").ShouldBeNull();
    }
}
