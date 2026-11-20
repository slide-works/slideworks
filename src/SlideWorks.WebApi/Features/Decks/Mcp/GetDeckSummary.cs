using System.Text.Json;
using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck with detail "summary": the same deck, with each slide reduced to its number, layout, part and headline.
public sealed record GetDeckSummary(
    string Id,
    string Slug,
    string Title,
    string? Subtitle,
    string? Author,
    IReadOnlyList<DeckPart> Parts,
    DeckSharing Sharing,
    string Url,
    GetDeckLook Look,
    [property: JsonPropertyName("talk_minutes")] int? TalkMinutes,
    IReadOnlyList<GetDeckSummarySlide> Slides)
{
    public static GetDeckSummary From(Deck deck, IEnumerable<Slide> slides, ResolvedTemplate template, string url)
        => new(
            deck.Id,
            deck.Slug,
            deck.Title,
            deck.Subtitle,
            deck.Author,
            DeckParts.Read(deck),
            deck.Sharing,
            url,
            GetDeckLook.From(deck, template),
            deck.TalkMinutes,
            slides.Select(GetDeckSummarySlide.From).ToList());
}

public sealed record GetDeckSummarySlide(int Number, string Layout, int? Part, string? Headline)
{
    public static GetDeckSummarySlide From(Slide slide)
        => new(slide.Position + 1, slide.Layout, slide.Part, HeadlineOf(SlideJson.Slots(slide)));

    // The line a summary shows: the headline, else the title, the question, or the quote.
    private static string? HeadlineOf(JsonElement slots)
    {
        foreach (var name in new[] { "headline", "title", "question" })
        {
            if (slots.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        if (!slots.TryGetProperty("quote", out var quote))
        {
            return null;
        }

        return quote.ValueKind switch
        {
            JsonValueKind.String => quote.GetString(),
            JsonValueKind.Object when quote.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String => text.GetString(),
            _ => null,
        };
    }
}
