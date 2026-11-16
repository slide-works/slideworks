using System.Text.Json;

namespace SlideWorks.UnitTest.Slides;

// src/contracts/sample-deck.json ("Ship smaller", K3), read once.
public static class SampleDeck
{
    private static readonly Lazy<JsonElement> _deck = new(() =>
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractFiles.PathOf("sample-deck.json")));
        return document.RootElement.Clone();
    });

    public static JsonElement Deck => _deck.Value;

    public static IEnumerable<JsonElement> Slides => Deck.GetProperty("slides").EnumerateArray();

    public static JsonElement Slide(int number) => Slides.Single(s => s.GetProperty("number").GetInt32() == number);
}
