using System.Text.Json.Serialization;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck: the whole deck for the agent that owns it (K6, L7), with its look, its talk length in minutes (P7, or null)
// and the live slides in order. url is absolute.
public sealed record GetDeckResult(
    string Id,
    string Slug,
    string Title,
    string? Subtitle,
    string? Author,
    IReadOnlyList<string> Parts,
    DeckSharing Sharing,
    string Url,
    GetDeckLook Look,
    [property: JsonPropertyName("talk_minutes")] int? TalkMinutes,
    IReadOnlyList<GetDeckSlide> Slides)
{
    public static GetDeckResult From(Deck deck, IEnumerable<Slide> slides, ResolvedTemplate template, string url)
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
            slides.Select(slide => GetDeckSlide.From(slide, template)).ToList());
}
