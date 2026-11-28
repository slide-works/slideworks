using System.Text.Json;
using SlideWorks.Core.Translations;

namespace SlideWorks.UnitTest.Translations;

// SlideTranslationsJson (K2): the stored form of a slide's lines.
[TestFixture]
public class SlideTranslationsJsonTests
{
    [Test]
    public void Write_WhenReadBack_ShouldRoundTrip()
    {
        var translations = new Dictionary<string, IReadOnlyDictionary<string, TranslationEntry>>(StringComparer.Ordinal)
        {
            ["th"] = new Dictionary<string, TranslationEntry>(StringComparer.Ordinal) { ["/title"] = new("ส่งให้เล็กลง", "Ship smaller") },
            ["zh-Hans"] = new Dictionary<string, TranslationEntry>(StringComparer.Ordinal)
            {
                ["/title"] = new("小步交付", "Ship smaller"),
                ["/items/0/text"] = new("风险来得晚", "Risk shows up late."),
            },
        };

        var json = SlideTranslationsJson.Write(translations)!;
        var read = SlideTranslationsJson.Read(json);

        read.Keys.ShouldBe(["th", "zh-Hans"]);
        read["th"]["/title"].ShouldBe(new TranslationEntry("ส่งให้เล็กลง", "Ship smaller"));
        read["zh-Hans"]["/items/0/text"].ShouldBe(new TranslationEntry("风险来得晚", "Risk shows up late."));
        read["zh-Hans"].Count.ShouldBe(2);
        JsonDocument.Parse(json).RootElement.GetProperty("th").GetProperty("/title").GetProperty("from").GetString().ShouldBe("Ship smaller");
    }

    [Test]
    public void Read_WhenNull_ShouldBeEmpty()
    {
        SlideTranslationsJson.Read(null).ShouldBeEmpty();
        SlideTranslationsJson.Read("").ShouldBeEmpty();
    }

    [Test]
    public void Write_WhenNoLanguages_ShouldBeNull()
    {
        SlideTranslationsJson.Write(new Dictionary<string, IReadOnlyDictionary<string, TranslationEntry>>()).ShouldBeNull();
    }
}
