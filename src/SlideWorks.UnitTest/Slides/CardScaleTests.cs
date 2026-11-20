using System.Text.Json;
using SlideWorks.Core.Slides;

namespace SlideWorks.UnitTest.Slides;

[TestFixture]
public class CardScaleTests
{
    [Test]
    public void IsLarge_WhenRunOverTheContractFixtures_ShouldAgreeWithEachOne()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("card-scale-fixtures.json")));
        var cases = file.RootElement.EnumerateArray().ToList();

        cases.Count.ShouldBe(12);
        foreach (var (fixture, index) in cases.Select((fixture, index) => (fixture, index)))
        {
            CardScale.IsLarge(fixture.GetProperty("cards")).ShouldBe(fixture.GetProperty("large").GetBoolean(), $"case {index}");
        }
    }

    [Test]
    public void IsLarge_WhenACardHasChips_ShouldBeFalse()
    {
        IsLarge("""[{ "title": "t", "text": "x" }, { "title": "t", "text": "x", "chips": ["one"] }]""").ShouldBeFalse();
        IsLarge("""[{ "title": "t", "text": "x" }, { "title": "t", "text": "x", "chips": [] }]""").ShouldBeTrue();
        IsLarge("""[{ "title": "t", "text": "x" }, { "title": "t", "text": "x", "chips": null }]""").ShouldBeTrue();
    }

    [Test]
    public void IsLarge_WhenATitleOrTextIsOverTheLimit_ShouldBeFalse()
    {
        var title = new string('t', CardScale.LARGE_TITLE);
        var text = new string('x', CardScale.LARGE_TEXT);

        IsLarge($$"""[{ "title": "{{title}}", "text": "{{text}}" }]""").ShouldBeTrue();
        IsLarge($$"""[{ "title": "{{title}}t", "text": "{{text}}" }]""").ShouldBeFalse();
        IsLarge($$"""[{ "title": "{{title}}", "text": "{{text}}x" }]""").ShouldBeFalse();
    }

    [Test]
    public void IsLarge_WhenTheValueIsNotAListOfCards_ShouldBeFalse()
    {
        IsLarge("\"cards\"").ShouldBeFalse();
        IsLarge("[7]").ShouldBeFalse();
    }

    private static bool IsLarge(string cards)
    {
        using var document = JsonDocument.Parse(cards);
        return CardScale.IsLarge(document.RootElement);
    }
}
