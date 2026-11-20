using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck with summary: the deck as get_deck has it, with each slide cut to its number, layout, part and headline.
public sealed record GetDeckSummaryResult(
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
    public static GetDeckSummaryResult From(GetDeckResult deck)
        => new(deck.Id, deck.Slug, deck.Title, deck.Subtitle, deck.Author, deck.Parts, deck.Sharing, deck.Url, deck.Look, deck.TalkMinutes, [.. deck.Slides.Select(GetDeckSummarySlide.From)]);
}
