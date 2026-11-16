using System.Text.Json;
using SlideWorks.Core.Look;
using SlideWorks.WebApi.Tests.Mcp;
using SlideWorks.WebApi.Tests.Slides;
using SlideWorks.WebApi.Tests.Support;

namespace SlideWorks.WebApi.Tests.Look;

// L7 list_palettes {size?, colour?}: size matches the swatch's length, colour matches a word ignoring case, and each palette
// comes with its tokens and contrast. No match is an empty list.
[TestFixture]
public class ListPalettesToolTests : ApiTestFixture
{
    private readonly PaletteCatalogue _palettes = new();

    [Test]
    public async Task ListPalettes_WhenNoFilter_ShouldReturnEveryPaletteWithTokensAndContrast()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var palettes = await ListAsync(agent, new { });

        Names(palettes).ShouldBe(_palettes.All.Select(p => p.Name));
        foreach (var (entry, palette) in palettes.Zip(_palettes.All))
        {
            entry.GetProperty("is_default").GetBoolean().ShouldBe(palette.IsDefault);
            entry.GetProperty("swatch").GetArrayLength().ShouldBe(palette.Swatch.Count);
            entry.GetProperty("tokens").GetProperty("accent").GetString().ShouldBe(palette.Tokens["accent"]);
            var contrast = entry.GetProperty("contrast");
            contrast.GetProperty("pass").ValueKind.ShouldBeOneOf(JsonValueKind.True, JsonValueKind.False);
            contrast.GetProperty("lowest").GetProperty("pair").GetString().ShouldNotBeNullOrEmpty();
            contrast.GetProperty("lowest").GetProperty("ratio").GetDouble().ShouldBeGreaterThan(0);
        }
    }

    [Test]
    public async Task ListPalettes_WhenColour_ShouldMatchItsWordsIgnoringCase()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var word = _palettes.All[0].Words[0];

        var palettes = await ListAsync(agent, new { colour = word.ToUpperInvariant() });

        Names(palettes).ShouldBe(_palettes.All.Where(p => p.Words.Contains(word)).Select(p => p.Name));
        palettes.Count.ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task ListPalettes_WhenSize_ShouldMatchTheSwatchLength()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);
        var size = _palettes.All[0].Swatch.Count;

        var palettes = await ListAsync(agent, new { size });

        Names(palettes).ShouldBe(_palettes.All.Where(p => p.Swatch.Count == size).Select(p => p.Name));
        (await ListAsync(agent, new { size = size + 1 })).ShouldBeEmpty();
    }

    [Test]
    public async Task ListPalettes_WhenNothingMatches_ShouldReturnAnEmptyList()
    {
        await using var agent = await McpTestClient.ConnectAsAsync(Factory, TestUser.Sam);

        var palettes = await ListAsync(agent, new { colour = "no-such-colour-word" });

        palettes.ShouldBeEmpty();
    }

    private static async Task<IReadOnlyList<JsonElement>> ListAsync(McpTestClient agent, object args)
        => (await SlideCalls.OkAsync(agent, "list_palettes", args)).GetProperty("palettes").EnumerateArray().ToList();

    private static IEnumerable<string?> Names(IEnumerable<JsonElement> palettes) => palettes.Select(p => p.GetProperty("name").GetString());
}
