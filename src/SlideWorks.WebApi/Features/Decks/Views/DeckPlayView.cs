using SlideWorks.WebApi.Features.Join.Views;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Slides;

namespace SlideWorks.WebApi.Features.Decks.Views;

// GET /api/play/{id}: everything the player needs in one request (deck-play.json, K4). The same shape as the sample
// deck (K3). It never contains notes. Look is the deck's resolved look (L6) and Join how the room reaches it (J1).
public sealed record DeckPlayView(
    string Id,
    string Slug,
    string Title,
    string? Author,
    IReadOnlyList<string> Parts,
    LookView Look,
    JoinView Join,
    IReadOnlyList<DeckPlaySlide> Slides)
{
    public static DeckPlayView From(Deck deck, IEnumerable<Slide> slides, LookView look, JoinView join)
        => new(deck.Id, deck.Slug, deck.Title, deck.Author, DeckParts.Read(deck), look, join, slides.Select(DeckPlaySlide.From).ToList());
}
