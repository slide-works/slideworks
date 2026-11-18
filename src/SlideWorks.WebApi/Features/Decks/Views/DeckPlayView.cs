using SlideWorks.WebApi.Features.Join.Views;
using SlideWorks.WebApi.Features.Look.Views;
using SlideWorks.WebApi.Features.Slides;
using SlideWorks.WebApi.Features.Templates;

namespace SlideWorks.WebApi.Features.Decks.Views;

// GET /api/play/{id}: everything the player needs in one request (deck-play.json, K4). The same shape as the sample
// deck (K3). It never contains notes. Look is the deck's resolved look (L6) and Join how the room reaches it (J1). IsOwner is true only for the signed-in person who owns the deck, the same rule as
// Go live (IDeckAccess.CanPresentAsync); it is false for every other viewer, and always on the render API.
public sealed record DeckPlayView(
    string Id,
    string Slug,
    string Title,
    string? Author,
    IReadOnlyList<string> Parts,
    LookView Look,
    JoinView Join,
    bool IsOwner,
    IReadOnlyList<DeckPlaySlide> Slides)
{
    public static DeckPlayView From(Deck deck, IEnumerable<Slide> slides, ResolvedTemplate template, LookView look, JoinView join, bool isOwner)
        => new(deck.Id, deck.Slug, deck.Title, deck.Author, DeckParts.Read(deck), look, join, isOwner, slides.Select(slide => DeckPlaySlide.From(slide, template)).ToList());
}
