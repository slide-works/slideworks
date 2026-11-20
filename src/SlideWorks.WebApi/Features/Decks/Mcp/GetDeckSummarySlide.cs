using System.Text.Json;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// One slide in a get_deck summary: its number, layout, part and a headline of at most 120 characters.
public sealed record GetDeckSummarySlide(int Number, string Layout, int? Part, string Headline)
{
    public const int HEADLINE_MAX = 120;

    private static readonly string[] _headlineSlots = ["headline", "title", "quote"];

    public static GetDeckSummarySlide From(GetDeckSlide slide)
        => new(slide.Number, slide.Layout, slide.Part, Cut(HeadlineOf(slide.Slots)));

    // slots.headline, else slots.title, else slots.quote, else "".
    private static string HeadlineOf(JsonElement slots)
    {
        foreach (var name in _headlineSlots)
        {
            if (slots.ValueKind == JsonValueKind.Object && slots.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? "";
            }
        }

        return "";
    }

    private static string Cut(string text) => text.Length <= HEADLINE_MAX ? text : text[..HEADLINE_MAX];
}
