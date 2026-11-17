using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Mcp;

// get_deck: the whole deck for the agent that owns it (K6, L7), with its look and the live slides in order. url is
// absolute.
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
    IReadOnlyList<GetDeckSlide> Slides)
{
    public static GetDeckResult From(Deck deck, IEnumerable<Slide> slides, string url)
        => new(
            deck.Id,
            deck.Slug,
            deck.Title,
            deck.Subtitle,
            deck.Author,
            DeckParts.Read(deck),
            deck.Sharing,
            url,
            GetDeckLook.From(deck),
            slides.Select(GetDeckSlide.From).ToList());
}
