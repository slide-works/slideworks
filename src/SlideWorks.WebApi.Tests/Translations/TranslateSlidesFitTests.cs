using SlideWorks.WebApi.Tests.Decks;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Translations;

// SW-883 K3, K4: translate_slides fit-checks the merged slide with the widths of the language's script (FitWidths.ForScript).
// The texts sit far from the bullets body's 620 px budget on both sides, so a change to a script's fitEm does not flip them.
[TestFixture]
[CancelAfter(60000)]
public class TranslateSlidesFitTests : ApiTestFixture
{
    [Test]
    public async Task TranslateSlides_WhenThaiLineTooLong_ShouldRefuseNamingSlideAndLanguage()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await BulletsDeckAsync(agent, 6, "th");
        // Six points of 190 Thai letters: three lines each at Thai's width, about 1,025 px of 620.
        var lines = Enumerable.Range(0, 6).ToDictionary(index => $"/items/{index}", _ => (string?)Repeat("กขคงจฉชซฌญ", 19));

        var (isError, json) = await TranslateAsync(agent, deck, "th", lines);

        isError.ShouldBeTrue();
        json.GetString()!.ShouldStartWith("slide 2 in Thai: ", Case.Sensitive);
        json.GetString()!.ShouldContain("too much text to fit on the slide", Case.Sensitive);
    }

    [Test]
    public async Task TranslateSlides_WhenJapaneseFitsInLatinButNotJapanese_ShouldRefuse()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, SignedInAs);
        var deck = await BulletsDeckAsync(agent, 4, "es", "ja");
        // Four points of 140 ideographs, every point of the slide: two lines each at the Latin width (about 479 px of 620), four at Japanese's (about 868 px).
        var lines = Enumerable.Range(0, 4).ToDictionary(index => $"/items/{index}", _ => (string?)Repeat("小さな変更を早く出す", 14));

        var (latinError, latin) = await TranslateAsync(agent, deck, "es", lines);
        var (isError, json) = await TranslateAsync(agent, deck, "ja", lines);

        latinError.ShouldBeFalse(latin.ToString());
        isError.ShouldBeTrue();
        json.GetString()!.ShouldStartWith("slide 2 in Japanese: ", Case.Sensitive);
        json.GetString()!.ShouldContain("too much text to fit on the slide", Case.Sensitive);
    }

    // A deck whose second slide is this many short bullets, with translations on in these languages.
    private async Task<string> BulletsDeckAsync(McpTestClient agent, int items, params string[] languages)
    {
        var deck = await DeckSeed.CreateDeckAsync(agent);
        await DeckSeed.AddQuoteAsync(agent, deck, "Small batches ship sooner");
        var points = string.Join(",", Enumerable.Range(1, items).Select(item => $"\"Point {item}\""));
        await DeckSeed.AddSlideAsync(agent, deck, "bullets", $$"""{"headline":"Ship small batches","items":[{{points}}]}""");
        await new TranslationRowSeed(Factory.Services).SettingsAsync(deck, true, "en", languages);
        return deck;
    }

    private static async Task<(bool IsError, System.Text.Json.JsonElement Json)> TranslateAsync(McpTestClient agent, string deck, string language, Dictionary<string, string?> lines)
    {
        var (isError, read) = await agent.CallToolAsync("get_deck", new { deck });
        isError.ShouldBeFalse(read.ToString());
        var revision = read.GetProperty("slides").EnumerateArray().Single(s => s.GetProperty("number").GetInt32() == 2).GetProperty("revision").GetInt64();
        return await agent.CallToolAsync("translate_slides", new { deck, language, slides = new[] { new { slide = 2, revision, lines } } });
    }

    private static string Repeat(string text, int times) => string.Concat(Enumerable.Repeat(text, times));
}
